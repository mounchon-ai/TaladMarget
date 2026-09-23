import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const cookieSet = vi.fn();
const redirect = vi.fn((to: string) => {
  throw new Error(`NEXT_REDIRECT ${to}`);
});
vi.mock("next/headers", () => ({ cookies: async () => ({ set: cookieSet }) }));
vi.mock("next/navigation", () => ({ redirect: (to: string) => redirect(to) }));

const { signIn } = await import("@/app/login/actions");
const { LoginForm } = await import("@/components/login-form");

function apiAnswers(status: number, body: unknown) {
  return vi.fn(async () => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } }));
}

function form(fields: Record<string, string>) {
  const fd = new FormData();
  for (const [k, v] of Object.entries(fields)) fd.set(k, v);
  return fd;
}

beforeEach(() => {
  cookieSet.mockClear();
  redirect.mockClear();
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FE-talad-002 · sign-in action (API-001)", () => {
  it("AC-talad-033 · correct password stores the token in an httpOnly cookie and goes to the sales page", async () => {
    vi.stubGlobal("fetch", apiAnswers(200, { accessToken: "jwt-somchai", expiresAt: "2026-09-24T08:00:00Z" }));

    await expect(signIn(undefined, form({ username: "somchai", password: "Somchai#2569", next: "/" }))).rejects.toThrow("NEXT_REDIRECT /");

    expect(cookieSet).toHaveBeenCalledWith("talad_session", "jwt-somchai", expect.objectContaining({ httpOnly: true, path: "/" }));
    expect(redirect).toHaveBeenCalledWith("/");
  });

  it("AC-talad-036 · after sign-in goes back to the page that was opened first", async () => {
    vi.stubGlobal("fetch", apiAnswers(200, { accessToken: "jwt-somchai", expiresAt: "2026-09-24T08:00:00Z" }));

    await expect(signIn(undefined, form({ username: "somchai", password: "Somchai#2569", next: "/history" }))).rejects.toThrow();

    expect(redirect).toHaveBeenCalledWith("/history");
  });

  it("AC-talad-034 · wrong password answers รหัสผ่านไม่ถูกต้อง under the password field and sets no cookie", async () => {
    vi.stubGlobal("fetch", apiAnswers(401, { code: "WRONG_PASSWORD", message: "รหัสผ่านไม่ถูกต้อง" }));

    const state = await signIn(undefined, form({ username: "somchai", password: "somchai2569", next: "/" }));

    expect(state).toEqual({ field: "password", message: "รหัสผ่านไม่ถูกต้อง" });
    expect(cookieSet).not.toHaveBeenCalled();
    expect(redirect).not.toHaveBeenCalled();
  });

  it("AC-talad-035 · unknown user answers ไม่พบชื่อผู้ใช้ under the username field", async () => {
    vi.stubGlobal("fetch", apiAnswers(401, { code: "USER_NOT_FOUND", message: "ไม่พบชื่อผู้ใช้" }));

    const state = await signIn(undefined, form({ username: "somchay", password: "x", next: "/" }));

    expect(state).toEqual({ field: "username", message: "ไม่พบชื่อผู้ใช้" });
  });

  it("a server nobody can reach asks the person to try again", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("fetch failed"); }));

    const state = await signIn(undefined, form({ username: "somchai", password: "x", next: "/" }));

    expect(state?.field).toBeNull();
    expect(state?.message).toContain("ลองใหม่");
  });
});

describe("FE-talad-002 · UI-talad-001 form", () => {
  it("draws the three controls with the wireframe's data-testid", () => {
    render(<LoginForm action={async () => undefined} next="/" />);

    expect(screen.getByTestId("ui-talad-001-ent-008-username")).toHaveProperty("type", "text");
    expect(screen.getByTestId("ui-talad-001-password")).toHaveProperty("type", "password");
    expect(screen.getByTestId("ui-talad-001-sign-in").textContent).toBe("เข้าสู่ระบบ");
  });

  it("AC-talad-034 · shows the message the action returned and stays on the form", async () => {
    const action = vi.fn(async () => ({ field: "password" as const, message: "รหัสผ่านไม่ถูกต้อง" }));
    render(<LoginForm action={action} next="/" />);

    await userEvent.type(screen.getByTestId("ui-talad-001-ent-008-username"), "somchai");
    await userEvent.type(screen.getByTestId("ui-talad-001-password"), "somchai2569");
    await userEvent.click(screen.getByTestId("ui-talad-001-sign-in"));

    expect(await screen.findByText("รหัสผ่านไม่ถูกต้อง")).toBeTruthy();
    expect(screen.getByTestId("ui-talad-001-sign-in")).toBeTruthy();
  });
});
