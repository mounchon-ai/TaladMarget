import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const redirect = vi.fn((to: string) => {
  throw new Error(`NEXT_REDIRECT ${to}`);
});
const search = vi.hoisted(() => ({ value: "" }));
vi.mock("next/headers", () => ({
  cookies: async () => ({ get: (name: string) => (name === "talad_session" ? { value: "jwt-somchai" } : undefined) }),
}));
vi.mock("next/navigation", () => ({
  redirect: (to: string) => redirect(to),
  useSearchParams: () => new URLSearchParams(search.value),
}));
const requireScreen = vi.fn<(screen: string) => Promise<{ username: string }>>(async () => ({ username: "somchai" }));
vi.mock("@/lib/me", () => ({ requireScreen: (screen: string) => requireScreen(screen) }));

const { saveMember } = await import("@/app/(app)/members/new/actions");
const { default: RegisterMemberPage } = await import("@/app/(app)/members/new/page");
const { RegisterMemberForm } = await import("@/components/register-member-form");
const { RegisteredNotice } = await import("@/components/registered-notice");
const { validateMember } = await import("@/lib/member-form");

const NAME_REQUIRED = "กรุณากรอกชื่อ";
const PHONE_FORMAT = "เบอร์โทรต้องเป็นตัวเลข 10 หลัก ขึ้นต้นด้วย 0";
const PHONE_TAKEN = "เบอร์โทรนี้เป็นสมาชิกอยู่แล้ว";

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
  redirect.mockClear();
  requireScreen.mockClear();
  search.value = "";
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FE-talad-010 · UI-talad-005 save action (API-013)", () => {
  it("AC-talad-005 · a good name and phone are posted with the session's token, then the members page says it worked", async () => {
    const fetch = apiAnswers(201, { id: 7, name: "สมหญิง ใจดี", phone: "0812345678", accumulatedAmount: 0, status: "Active" });
    vi.stubGlobal("fetch", fetch);

    await expect(saveMember(undefined, form("สมหญิง ใจดี", "0812345678"))).rejects.toThrow("NEXT_REDIRECT /members?registered=1");

    const [url, init] = fetch.mock.calls[0];
    expect(new URL(url).pathname).toBe("/api/members");
    expect(init?.method).toBe("POST");
    expect(JSON.parse(String(init?.body))).toEqual({ name: "สมหญิง ใจดี", phone: "0812345678" });
    expect(new Headers(init?.headers).get("authorization")).toBe("Bearer jwt-somchai");
  });

  // the api alone knows whether the phone belongs to a hidden member (FE-talad-009); the page follows its 201
  it.each([
    ["AC-talad-011", "สมศรี มีสุข", "0812345678"],
    ["AC-talad-040", "ปรีชา ดีงาม", "0861112222"],
    ["AC-talad-090", "ปรีชา ดีงาม", "0861112222"],
  ])("%s · the api registers %s at %s and the page goes to the members page", async (_ac, name, phone) => {
    vi.stubGlobal("fetch", apiAnswers(201, { id: 9, name, phone, accumulatedAmount: 0, status: "Active" }));

    await expect(saveMember(undefined, form(name, phone))).rejects.toThrow("NEXT_REDIRECT /members?registered=1");
  });

  it("AC-talad-006 · no name is answered under the name field and the api is never asked", async () => {
    const fetch = apiAnswers(201, {});
    vi.stubGlobal("fetch", fetch);

    const state = await saveMember(undefined, form("", "0898765432"));

    expect(state).toEqual({ values: { name: "", phone: "0898765432" }, errors: { name: NAME_REQUIRED } });
    expect(fetch).not.toHaveBeenCalled();
    expect(redirect).not.toHaveBeenCalled();
  });

  it("AC-talad-007 · a nine-digit phone is answered under the phone field and the api is never asked", async () => {
    const fetch = apiAnswers(201, {});
    vi.stubGlobal("fetch", fetch);

    const state = await saveMember(undefined, form("มานะ ขยัน", "081234567"));

    expect(state?.errors).toEqual({ phone: PHONE_FORMAT });
    expect(fetch).not.toHaveBeenCalled();
  });

  it("AC-talad-009 · a phone an active member holds comes back from the api under the phone field, typed values kept", async () => {
    vi.stubGlobal("fetch", apiAnswers(409, { code: "PHONE_TAKEN", errors: [{ field: "phone", message: PHONE_TAKEN }] }));

    const state = await saveMember(undefined, form("สมศรี มีสุข", "0812345678"));

    expect(state).toEqual({ values: { name: "สมศรี มีสุข", phone: "0812345678" }, errors: { phone: PHONE_TAKEN } });
    expect(redirect).not.toHaveBeenCalled();
  });

  it("an expired session goes to sign-out rather than a message", async () => {
    vi.stubGlobal("fetch", apiAnswers(401, {}));

    await expect(saveMember(undefined, form("สมหญิง ใจดี", "0812345678"))).rejects.toThrow("NEXT_REDIRECT /logout");
  });

  it("a server nobody can reach asks the person to try again and keeps what was typed", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("fetch failed"); }));

    const state = await saveMember(undefined, form("สมหญิง ใจดี", "0812345678"));

    expect(state?.message).toContain("ลองใหม่");
    expect(state?.values).toEqual({ name: "สมหญิง ใจดี", phone: "0812345678" });
  });
});

describe("FE-talad-010 · BR-talad-002@v1 at the form", () => {
  it("answers both fields at once, not just the first", () => {
    expect(validateMember("  ", "")).toEqual({ name: NAME_REQUIRED, phone: PHONE_FORMAT });
  });

  it("takes dashes and spaces out of the phone before checking", () => {
    expect(validateMember("สมหญิง ใจดี", "081-234 5678")).toEqual({});
  });

  it.each(["1812345678", "08123456789", "08l2345678", "๐๘๑๒๓๔๕๖๗๘"])("refuses %s — ten ASCII digits from 0 only", (phone) => {
    expect(validateMember("มานะ ขยัน", phone)).toEqual({ phone: PHONE_FORMAT });
  });
});

describe("FE-talad-010 · UI-talad-005 page and form", () => {
  it("AC-talad-045 · the page asks for its own screen before drawing anything", async () => {
    render(await RegisterMemberPage());

    expect(requireScreen).toHaveBeenCalledWith("UI-talad-005");
    expect(screen.getByRole("heading", { name: "สมัครสมาชิก" })).toBeTruthy();
  });

  it("draws the four controls with the wireframe's data-testid; cancel goes to the members page", () => {
    render(<RegisterMemberForm action={async () => undefined} />);

    expect(screen.getByTestId("ui-talad-005-ent-004-name")).toHaveProperty("name", "name");
    expect(screen.getByTestId("ui-talad-005-ent-004-phone")).toHaveProperty("name", "phone");
    expect(screen.getByTestId("ui-talad-005-save").textContent).toBe("บันทึก");
    expect(screen.getByTestId("ui-talad-005-cancel").getAttribute("href")).toBe("/members");
  });

  it("AC-talad-006 · the message sits under the name field and what was typed stays", async () => {
    const action = vi.fn(async (_prev: unknown, fd: FormData) => ({
      values: { name: String(fd.get("name")), phone: String(fd.get("phone")) },
      errors: { name: NAME_REQUIRED },
    }));
    render(<RegisterMemberForm action={action} />);

    await userEvent.type(screen.getByTestId("ui-talad-005-ent-004-phone"), "0898765432");
    await userEvent.click(screen.getByTestId("ui-talad-005-save"));

    const message = await screen.findByText(NAME_REQUIRED);
    expect(message.closest("label")?.querySelector("[data-testid=ui-talad-005-ent-004-name]")).toBeTruthy();
    expect((screen.getByTestId("ui-talad-005-ent-004-phone") as HTMLInputElement).value).toBe("0898765432");
  });

  it("the browser's own required check is off, so the declared sentence is what shows", () => {
    render(<RegisterMemberForm action={async () => undefined} />);

    expect(screen.getByTestId("ui-talad-005-save").closest("form")?.noValidate).toBe(true);
  });

  it("state loading · save is off while saving", async () => {
    let finish: () => void = () => {};
    const action = vi.fn(() => new Promise<undefined>((resolve) => (finish = () => resolve(undefined))));
    render(<RegisterMemberForm action={action} />);

    await userEvent.click(screen.getByTestId("ui-talad-005-save"));

    expect((screen.getByTestId("ui-talad-005-save") as HTMLButtonElement).disabled).toBe(true);
    finish();
  });

  it("AC-talad-005 · the frame says สมัครสมาชิกสำเร็จ after a save", () => {
    search.value = "registered=1";

    render(<RegisteredNotice />);

    expect(screen.getByRole("status").textContent).toBe("สมัครสมาชิกสำเร็จ");
  });
});
