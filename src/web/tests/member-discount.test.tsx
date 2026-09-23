import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const revalidatePath = vi.hoisted(() => vi.fn());
const redirect = vi.hoisted(() =>
  vi.fn((to: string) => {
    throw new Error(`NEXT_REDIRECT ${to}`);
  }),
);
const requireScreen = vi.hoisted(() => vi.fn<(screen: string) => Promise<{ username: string }>>(async () => ({ username: "owner" })));
vi.mock("next/cache", () => ({ revalidatePath }));
vi.mock("next/headers", () => ({ cookies: async () => ({ get: () => ({ value: "jwt-owner" }) }) }));
vi.mock("next/navigation", () => ({ redirect: (to: string) => redirect(to) }));
vi.mock("@/lib/me", () => ({ requireScreen: (s: string) => requireScreen(s) }));

const { saveMemberDiscount } = await import("@/app/(app)/member-discount/actions");
const { default: MemberDiscountPage } = await import("@/app/(app)/member-discount/page");
const { MemberDiscountForm } = await import("@/components/member-discount-form");

const RATE_OUT_OF_RANGE = "ส่วนลดสมาชิกต้องเป็นจำนวนเต็ม 0–100";
const neverSet = { ratePercent: 0, changedById: null, changedByName: null, changedAt: null };
const fivePercent = { ratePercent: 5, changedById: 1, changedByName: "เจ้าของร้าน", changedAt: "2026-09-23T08:00:00Z" };
const tenPercent = { ...fivePercent, ratePercent: 10, changedAt: "2026-09-23T09:30:00Z" };

function apiAnswers(status: number, body: unknown) {
  return vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(
    async () => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } }),
  );
}

function form(rate: string) {
  const fd = new FormData();
  fd.set("ratePercent", rate);
  return fd;
}

beforeEach(() => {
  revalidatePath.mockClear();
  redirect.mockClear();
  requireScreen.mockClear();
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FE-talad-030 · UI-talad-017 save action (API-031)", () => {
  it("AC-talad-061 · 10% is sent as a number with the owner's token and becomes the one in force", async () => {
    const fetch = apiAnswers(201, tenPercent);
    vi.stubGlobal("fetch", fetch);

    const state = await saveMemberDiscount(fivePercent, undefined, form("10"));

    const [url, init] = fetch.mock.calls[0];
    expect([new URL(url).pathname, init?.method]).toEqual(["/api/settings/member-discount", "POST"]);
    expect(JSON.parse(String(init?.body))).toEqual({ ratePercent: 10 });
    expect(new Headers(init?.headers).get("authorization")).toBe("Bearer jwt-owner");
    expect(state).toEqual({ current: tenPercent, typed: "10" });
    expect(revalidatePath).toHaveBeenCalledWith("/member-discount");
  });

  it.each([["101", 101], ["5.5", 5.5], ["", null], ["abc", null]])(
    "state error · %s goes to the api as typed, the rule's sentence sits under the field and 5%% stays",
    async (typed, sent) => {
      const fetch = apiAnswers(400, { code: "RATE_OUT_OF_RANGE", errors: [{ field: "ratePercent", message: RATE_OUT_OF_RANGE }] });
      vi.stubGlobal("fetch", fetch);

      const state = await saveMemberDiscount(fivePercent, undefined, form(typed));

      expect(JSON.parse(String(fetch.mock.calls[0][1]?.body))).toEqual({ ratePercent: sent });
      expect(state).toEqual({ current: fivePercent, typed, error: RATE_OUT_OF_RANGE });
      expect(revalidatePath).not.toHaveBeenCalled();
    },
  );

  it("a refusal after a save that went through keeps the newer % in force, not the one the page opened with", async () => {
    vi.stubGlobal("fetch", apiAnswers(400, { code: "RATE_OUT_OF_RANGE", errors: [{ field: "ratePercent", message: RATE_OUT_OF_RANGE }] }));

    const state = await saveMemberDiscount(fivePercent, { current: tenPercent, typed: "10" }, form("200"));

    expect(state?.current).toEqual(tenPercent);
  });

  it("an owner who lost the role gets the no-permission sentence; an expired session signs out", async () => {
    vi.stubGlobal("fetch", apiAnswers(403, {}));
    expect((await saveMemberDiscount(fivePercent, undefined, form("10")))?.message).toBe("คุณไม่มีสิทธิ์เข้าถึงหน้านี้");

    vi.stubGlobal("fetch", apiAnswers(401, {}));
    await expect(saveMemberDiscount(fivePercent, undefined, form("10"))).rejects.toThrow("NEXT_REDIRECT /logout");
  });
});

describe("FE-talad-030 · UI-talad-017 page and form", () => {
  it("AC-talad-045 · asks for its own screen first, then shows the % in force, who set it and when (Thai time)", async () => {
    vi.stubGlobal("fetch", apiAnswers(200, fivePercent));

    render(await MemberDiscountPage());

    expect(requireScreen).toHaveBeenCalledWith("UI-talad-017");
    expect((screen.getByTestId("ui-talad-017-ent-007-rate-percent") as HTMLInputElement).value).toBe("5");
    expect(screen.getByTestId("ui-talad-017-ent-007-changed-by").textContent).toBe("เจ้าของร้าน");
    expect(screen.getByTestId("ui-talad-017-ent-007-changed-at").textContent).toContain("15:00");
    expect(screen.queryByText("ยังไม่มีส่วนลดสมาชิก")).toBeNull();
  });

  it("state unauthorized · a seller is sent away before the setting is read", async () => {
    const fetch = apiAnswers(200, fivePercent);
    vi.stubGlobal("fetch", fetch);
    requireScreen.mockRejectedValueOnce(new Error("NEXT_REDIRECT /?denied=1"));

    await expect(MemberDiscountPage()).rejects.toThrow("NEXT_REDIRECT /?denied=1");
    expect(fetch).not.toHaveBeenCalled();
  });

  it("state empty · never set shows 0% and ยังไม่มีส่วนลดสมาชิก", () => {
    render(<MemberDiscountForm initial={neverSet} action={async () => undefined} />);

    expect((screen.getByTestId("ui-talad-017-ent-007-rate-percent") as HTMLInputElement).value).toBe("0");
    expect(screen.getByText("ยังไม่มีส่วนลดสมาชิก")).toBeTruthy();
    expect(screen.getByTestId("ui-talad-017-ent-007-changed-by").textContent).toBe("—");
  });

  it("state error · the sentence sits under the % field, what was typed stays, and 5% is still shown as set", async () => {
    const action = vi.fn(async (_prev: unknown, fd: FormData) => ({ current: fivePercent, typed: String(fd.get("ratePercent")), error: RATE_OUT_OF_RANGE }));
    render(<MemberDiscountForm initial={fivePercent} action={action} />);

    const box = screen.getByTestId("ui-talad-017-ent-007-rate-percent") as HTMLInputElement;
    await userEvent.clear(box);
    await userEvent.type(box, "101");
    await userEvent.click(screen.getByTestId("ui-talad-017-save"));

    const message = await screen.findByText(RATE_OUT_OF_RANGE);
    expect(message.closest("label")?.contains(screen.getByTestId("ui-talad-017-ent-007-rate-percent"))).toBe(true);
    expect((screen.getByTestId("ui-talad-017-ent-007-rate-percent") as HTMLInputElement).value).toBe("101");
    expect(screen.getByTestId("ui-talad-017-save").closest("form")?.noValidate).toBe(true);
  });

  it("AC-talad-061 · after saving 10% the form shows 10% and the new time", async () => {
    render(<MemberDiscountForm initial={fivePercent} action={async () => ({ current: tenPercent, typed: "10" })} />);

    await userEvent.click(screen.getByTestId("ui-talad-017-save"));

    expect((await screen.findByDisplayValue("10")).getAttribute("data-testid")).toBe("ui-talad-017-ent-007-rate-percent");
    expect(screen.getByTestId("ui-talad-017-ent-007-changed-at").textContent).toContain("16:30");
  });

  it("state loading · save is off while saving", async () => {
    let finish: () => void = () => {};
    const action = vi.fn(() => new Promise<undefined>((resolve) => (finish = () => resolve(undefined))));
    render(<MemberDiscountForm initial={fivePercent} action={action} />);

    await userEvent.click(screen.getByTestId("ui-talad-017-save"));

    expect((screen.getByTestId("ui-talad-017-save") as HTMLButtonElement).disabled).toBe(true);
    finish();
  });
});
