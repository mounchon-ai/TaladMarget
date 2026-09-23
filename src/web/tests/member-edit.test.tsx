import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const revalidatePath = vi.hoisted(() => vi.fn());
const redirect = vi.hoisted(() =>
  vi.fn((to: string) => {
    throw new Error(`NEXT_REDIRECT ${to}`);
  }),
);
const search = vi.hoisted(() => ({ value: "" }));
const requireScreen = vi.hoisted(() => vi.fn<(screen: string) => Promise<{ username: string }>>(async () => ({ username: "somchai" })));
vi.mock("next/cache", () => ({ revalidatePath }));
vi.mock("next/headers", () => ({ cookies: async () => ({ get: () => ({ value: "jwt-somchai" }) }) }));
vi.mock("next/navigation", () => ({
  redirect: (to: string) => redirect(to),
  useSearchParams: () => new URLSearchParams(search.value),
}));
vi.mock("@/lib/me", () => ({ requireScreen: (s: string) => requireScreen(s) }));

const { saveMemberEdit } = await import("@/app/(app)/members/[id]/actions");
const { default: MemberPage } = await import("@/app/(app)/members/[id]/page");
const { EditMemberForm } = await import("@/components/edit-member-form");
const { MemberMissingNotice } = await import("@/components/registered-notice");

const PHONE_FORMAT = "เบอร์โทรต้องเป็นตัวเลข 10 หลัก ขึ้นต้นด้วย 0";
const PHONE_TAKEN = "เบอร์โทรนี้เป็นสมาชิกอยู่แล้ว";
const somying = { id: 11, name: "สมหญิง ใจดี", phone: "0812345678", accumulatedAmount: 1500, status: "Active" };

function apiAnswers(status: number, body: unknown) {
  return vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(
    async () => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } }),
  );
}

function form(name: string, phone: string) {
  const fd = new FormData();
  fd.set("name", name);
  fd.set("phone", phone);
  return fd;
}

beforeEach(() => {
  revalidatePath.mockClear();
  redirect.mockClear();
  requireScreen.mockClear();
  search.value = "";
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FE-talad-014 · UI-talad-006 save action (API-015)", () => {
  it("AC-talad-043 · a new name is saved with the phone that was already there, and the page says it went through", async () => {
    const fetch = apiAnswers(200, { ...somying, name: "สมหญิง ใจงาม" });
    vi.stubGlobal("fetch", fetch);

    const state = await saveMemberEdit(11, undefined, form("สมหญิง ใจงาม", "0812345678"));

    const [url, init] = fetch.mock.calls[0];
    expect([new URL(url).pathname, init?.method]).toEqual(["/api/members/11", "PUT"]);
    expect(JSON.parse(String(init?.body))).toEqual({ name: "สมหญิง ใจงาม", phone: "0812345678" });
    expect(new Headers(init?.headers).get("authorization")).toBe("Bearer jwt-somchai");
    expect(state).toEqual({ values: { name: "สมหญิง ใจงาม", phone: "0812345678" }, errors: {}, saved: true });
    expect(revalidatePath).toHaveBeenCalledWith("/members");
  });

  it("AC-talad-008 · an eleven-digit phone is answered under the phone field and the api is never asked", async () => {
    const fetch = apiAnswers(200, somying);
    vi.stubGlobal("fetch", fetch);

    const state = await saveMemberEdit(11, undefined, form("สมหญิง ใจดี", "08123456789"));

    expect(state).toEqual({ values: { name: "สมหญิง ใจดี", phone: "08123456789" }, errors: { phone: PHONE_FORMAT } });
    expect(fetch).not.toHaveBeenCalled();
  });

  it("AC-talad-010 · a phone another member holds comes back under the phone field, what was typed kept", async () => {
    vi.stubGlobal("fetch", apiAnswers(409, { code: "PHONE_TAKEN", errors: [{ field: "phone", message: PHONE_TAKEN }] }));

    const state = await saveMemberEdit(12, undefined, form("มานะ ขยัน", "0812345678"));

    expect(state).toEqual({ values: { name: "มานะ ขยัน", phone: "0812345678" }, errors: { phone: PHONE_TAKEN } });
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it("state error · a member hidden since the page opened sends the person back to the list", async () => {
    vi.stubGlobal("fetch", apiAnswers(404, { code: "MEMBER_NOT_FOUND", errors: [] }));

    await expect(saveMemberEdit(11, undefined, form("สมหญิง ใจงาม", "0812345678"))).rejects.toThrow("NEXT_REDIRECT /members?missing=1");
  });

  it("an expired session goes to sign-out; an unreachable server asks to try again", async () => {
    vi.stubGlobal("fetch", apiAnswers(401, {}));
    await expect(saveMemberEdit(11, undefined, form("สมหญิง ใจงาม", "0812345678"))).rejects.toThrow("NEXT_REDIRECT /logout");

    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("fetch failed"); }));
    const state = await saveMemberEdit(11, undefined, form("สมหญิง ใจงาม", "0812345678"));
    expect(state?.message).toContain("ลองใหม่");
    expect(state?.values.name).toBe("สมหญิง ใจงาม");
  });
});

describe("FE-talad-014 · UI-talad-006 page and form", () => {
  it("AC-talad-045 · asks for its own screen first, then draws the member's name, whole phone and amount bought", async () => {
    vi.stubGlobal("fetch", apiAnswers(200, somying));

    render(await MemberPage({ params: Promise.resolve({ id: "11" }) }));

    expect(requireScreen).toHaveBeenCalledWith("UI-talad-006");
    expect((screen.getByTestId("ui-talad-006-ent-004-name") as HTMLInputElement).value).toBe("สมหญิง ใจดี");
    expect((screen.getByTestId("ui-talad-006-ent-004-phone") as HTMLInputElement).value).toBe("0812345678");
    expect(screen.getByTestId("ui-talad-006-ent-004-accumulated-amount").textContent).toBe("฿1,500.00");
    expect(screen.getByTestId("ui-talad-006-back").getAttribute("href")).toBe("/members");
  });

  it("AC-talad-043 · the page has no delete-member button (FE-talad-016 adds it, for the owner only)", async () => {
    vi.stubGlobal("fetch", apiAnswers(200, somying));

    render(await MemberPage({ params: Promise.resolve({ id: "11" }) }));

    expect(screen.queryByTestId("ui-talad-006-hide-member")).toBeNull();
  });

  it.each([
    ["a hidden or unknown member", "11", 404],
    ["an id that is not a number", "abc", 200],
  ])("state error · %s goes back to the list with ไม่พบสมาชิก", async (_case, id, status) => {
    vi.stubGlobal("fetch", apiAnswers(status, status === 404 ? { code: "MEMBER_NOT_FOUND", errors: [] } : somying));

    await expect(MemberPage({ params: Promise.resolve({ id }) })).rejects.toThrow("NEXT_REDIRECT /members?missing=1");
  });

  it("state error · the refusal sits under its field and the typed values stay", async () => {
    const action = vi.fn(async (_prev: unknown, fd: FormData) => ({
      values: { name: String(fd.get("name")), phone: String(fd.get("phone")) },
      errors: { phone: PHONE_FORMAT },
    }));
    render(<EditMemberForm member={somying} action={action} />);

    const phone = screen.getByTestId("ui-talad-006-ent-004-phone") as HTMLInputElement;
    await userEvent.clear(phone);
    await userEvent.type(phone, "08123456789");
    await userEvent.click(screen.getByTestId("ui-talad-006-save"));

    const message = await screen.findByText(PHONE_FORMAT);
    expect(message.closest("label")?.querySelector("[data-testid=ui-talad-006-ent-004-phone]")).toBeTruthy();
    expect((screen.getByTestId("ui-talad-006-ent-004-phone") as HTMLInputElement).value).toBe("08123456789");
    expect(screen.getByTestId("ui-talad-006-save").closest("form")?.noValidate).toBe(true);
  });

  it("AC-talad-043 · a save that went through says บันทึกสำเร็จ and shows the new name", async () => {
    const action = vi.fn(async () => ({ values: { name: "สมหญิง ใจงาม", phone: "0812345678" }, errors: {}, saved: true }));
    render(<EditMemberForm member={somying} action={action} />);

    await userEvent.click(screen.getByTestId("ui-talad-006-save"));

    expect((await screen.findByRole("status")).textContent).toBe("บันทึกสำเร็จ");
    expect((screen.getByTestId("ui-talad-006-ent-004-name") as HTMLInputElement).value).toBe("สมหญิง ใจงาม");
  });

  it("state loading · save is off while saving", async () => {
    let finish: () => void = () => {};
    const action = vi.fn(() => new Promise<undefined>((resolve) => (finish = () => resolve(undefined))));
    render(<EditMemberForm member={somying} action={action} />);

    await userEvent.click(screen.getByTestId("ui-talad-006-save"));

    expect((screen.getByTestId("ui-talad-006-save") as HTMLButtonElement).disabled).toBe(true);
    finish();
  });

  it("state error · the list says ไม่พบสมาชิก after being sent back", () => {
    search.value = "missing=1";

    render(<MemberMissingNotice />);

    expect(screen.getByRole("alert").textContent).toBe("ไม่พบสมาชิก");
  });
});
