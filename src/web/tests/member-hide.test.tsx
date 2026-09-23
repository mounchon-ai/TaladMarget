import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

const revalidatePath = vi.hoisted(() => vi.fn());
const redirect = vi.hoisted(() =>
  vi.fn((to: string) => {
    throw new Error(`NEXT_REDIRECT ${to}`);
  }),
);
const me = vi.hoisted(() => ({ role: "Owner" }));
vi.mock("next/cache", () => ({ revalidatePath }));
vi.mock("next/headers", () => ({ cookies: async () => ({ get: () => ({ value: "jwt-owner" }) }) }));
vi.mock("next/navigation", () => ({ redirect: (to: string) => redirect(to) }));
vi.mock("@/lib/me", () => ({ requireScreen: async () => ({ username: "u", role: me.role }) }));

const { hideMemberAction } = await import("@/app/(app)/members/[id]/actions");
const { default: MemberPage } = await import("@/app/(app)/members/[id]/page");
const { HideMemberZone } = await import("@/components/hide-member-zone");

const somying = { id: 11, name: "สมหญิง ใจดี", phone: "0812345678", accumulatedAmount: 250, status: "Active" };

function apiAnswers(status: number, body?: unknown) {
  return vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(
    async () => new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { "content-type": "application/json" } }),
  );
}

beforeAll(() => {
  // jsdom draws <dialog> but does not implement showModal/close
  HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) { this.open = true; };
  HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) { this.open = false; };
});
beforeEach(() => {
  revalidatePath.mockClear();
  redirect.mockClear();
  me.role = "Owner";
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FE-talad-016 · UI-talad-006 hide-member action (API-016)", () => {
  it.each(["AC-talad-012", "AC-talad-089"])("%s · the owner's hide goes to the api and back to the members list", async () => {
    const fetch = apiAnswers(204);
    vi.stubGlobal("fetch", fetch);

    await expect(hideMemberAction(11)).rejects.toThrow("NEXT_REDIRECT /members");

    const [url, init] = fetch.mock.calls[0];
    expect([new URL(url).pathname, init?.method]).toEqual(["/api/members/11/hide", "POST"]);
    expect(new Headers(init?.headers).get("authorization")).toBe("Bearer jwt-owner");
    expect(revalidatePath).toHaveBeenCalledWith("/members");
    expect(redirect).toHaveBeenCalledWith("/members");
  });

  it("BR-talad-019@v1 · a seller refused by the api gets the no-permission sentence and stays", async () => {
    vi.stubGlobal("fetch", apiAnswers(403));

    expect(await hideMemberAction(11)).toEqual({ ok: false, message: "คุณไม่มีสิทธิ์เข้าถึงหน้านี้" });
    expect(redirect).not.toHaveBeenCalled();
  });

  it("a member already hidden sends the person back with ไม่พบสมาชิก", async () => {
    vi.stubGlobal("fetch", apiAnswers(404, { code: "MEMBER_NOT_FOUND", errors: [] }));

    await expect(hideMemberAction(11)).rejects.toThrow("NEXT_REDIRECT /members?missing=1");
  });

  it("an expired session goes to sign-out; an unreachable server asks to try again", async () => {
    vi.stubGlobal("fetch", apiAnswers(401));
    await expect(hideMemberAction(11)).rejects.toThrow("NEXT_REDIRECT /logout");

    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("fetch failed"); }));
    expect((await hideMemberAction(11)).message).toContain("ลองใหม่");
  });
});

describe("FE-talad-016 · UI-talad-006 danger zone", () => {
  it("the owner sees ลบสมาชิก on the member page", async () => {
    vi.stubGlobal("fetch", apiAnswers(200, somying));

    render(await MemberPage({ params: Promise.resolve({ id: "11" }) }));

    expect(screen.getByTestId("ui-talad-006-hide-member").textContent).toBe("ลบสมาชิก");
  });

  it("AC-talad-043 · BR-talad-019@v1 · a seller does not see ลบสมาชิก", async () => {
    me.role = "Cashier";
    vi.stubGlobal("fetch", apiAnswers(200, somying));

    render(await MemberPage({ params: Promise.resolve({ id: "11" }) }));

    expect(screen.queryByTestId("ui-talad-006-hide-member")).toBeNull();
    expect(screen.getByTestId("ui-talad-006-save")).toBeTruthy();
  });

  it("ลบสมาชิก asks first; ลบ hides, ยกเลิก does not", async () => {
    const hide = vi.fn(async () => ({ ok: false as const, message: "" }));
    render(<HideMemberZone name="สมหญิง ใจดี" hide={hide} />);

    await userEvent.click(screen.getByTestId("ui-talad-006-hide-member"));
    expect(screen.getByText("ต้องการลบสมาชิก สมหญิง ใจดี หรือไม่?")).toBeTruthy();
    expect(hide).not.toHaveBeenCalled();

    await userEvent.click(screen.getByText("ยกเลิก"));
    expect(hide).not.toHaveBeenCalled();

    await userEvent.click(screen.getByTestId("ui-talad-006-hide-member"));
    await userEvent.click(screen.getByText("ลบ"));
    expect(hide).toHaveBeenCalledTimes(1);
  });

  it("a refusal is shown in the danger zone", async () => {
    render(<HideMemberZone name="สมหญิง ใจดี" hide={async () => ({ ok: false, message: "คุณไม่มีสิทธิ์เข้าถึงหน้านี้" })} />);

    await userEvent.click(screen.getByTestId("ui-talad-006-hide-member"));
    await userEvent.click(screen.getByText("ลบ"));

    expect((await screen.findByRole("alert")).textContent).toBe("คุณไม่มีสิทธิ์เข้าถึงหน้านี้");
  });
});
