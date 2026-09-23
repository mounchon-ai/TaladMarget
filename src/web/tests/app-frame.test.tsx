import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const nav = vi.hoisted(() => ({
  pathname: "/",
  search: new URLSearchParams(),
  redirect: vi.fn((to: string) => {
    throw new Error(`NEXT_REDIRECT ${to}`);
  }),
}));
const jar = vi.hoisted(() => ({ token: "jwt-somchai" as string | undefined, deleted: [] as string[] }));

vi.mock("next/navigation", () => ({
  usePathname: () => nav.pathname,
  useSearchParams: () => nav.search,
  redirect: (to: string) => nav.redirect(to),
}));
vi.mock("next/headers", () => ({
  cookies: async () => ({
    get: (name: string) => (name === "talad_session" && jar.token ? { value: jar.token } : undefined),
    delete: (name: string) => jar.deleted.push(name),
  }),
}));

const { AppFrame } = await import("@/components/app-frame");
const { DeniedNotice } = await import("@/components/denied-notice");
const { requireScreen } = await import("@/lib/me");
const { decideRoute } = await import("@/lib/auth-routing");
const logout = await import("@/app/logout/route");

const cashier = {
  username: "somchai",
  displayName: "สมชาย",
  role: "Cashier",
  menu: [
    { screen: "UI-talad-002", label: "หน้าขาย" },
    { screen: "UI-talad-004", label: "สมาชิก" },
    { screen: "UI-talad-007", label: "ประวัติการขาย" },
  ],
  screens: ["UI-talad-002", "UI-talad-003", "RPT-talad-001", "UI-talad-004", "UI-talad-005", "UI-talad-006", "UI-talad-007", "UI-talad-008"],
};
const ownerMenu = [
  ["UI-talad-002", "หน้าขาย"], ["UI-talad-004", "สมาชิก"], ["UI-talad-007", "ประวัติการขาย"], ["UI-talad-010", "สต็อก"],
  ["UI-talad-015", "โปรโมชั่น"], ["UI-talad-017", "ส่วนลดสมาชิก"], ["RPT-talad-002", "รายงานยอดขาย"],
  ["RPT-talad-003", "รายงานสินค้าขายดี"], ["RPT-talad-004", "รายงานยอดขายแยกตามผู้ขาย"], ["RPT-talad-005", "รายงานสต็อกคงเหลือ"],
  ["UI-talad-018", "บัญชีพนักงาน"],
].map(([screen, label]) => ({ screen, label }));
const owner = { ...cashier, username: "owner", displayName: "เจ้าของร้าน", role: "Owner", menu: ownerMenu, screens: ownerMenu.map((m) => m.screen) };

function apiMe(status: number, body?: unknown) {
  return vi.fn(async () => new Response(body === undefined ? null : JSON.stringify(body), { status }));
}

beforeEach(() => {
  nav.pathname = "/";
  nav.search = new URLSearchParams();
  nav.redirect.mockClear();
  jar.token = "jwt-somchai";
  jar.deleted = [];
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FE-talad-004 · AC-talad-044 · the left menu shows what the role may open", () => {
  it("a cashier sees only หน้าขาย · สมาชิก · ประวัติการขาย", () => {
    render(<AppFrame me={cashier}>page</AppFrame>);

    const links = screen.getAllByRole("link");
    expect(links.map((a) => a.textContent)).toEqual(["หน้าขาย", "สมาชิก", "ประวัติการขาย"]);
    expect(links.map((a) => a.getAttribute("data-testid"))).toEqual(["nav-ui-talad-002", "nav-ui-talad-004", "nav-ui-talad-007"]);
    expect(screen.queryByTestId("nav-ui-talad-010")).toBeNull();
    expect(screen.queryByText("สินค้า")).toBeNull();
    expect(screen.queryByText("รายงาน")).toBeNull();
  });

  it("the owner sees every menu, the sales work included", () => {
    render(<AppFrame me={owner}>page</AppFrame>);

    expect(screen.getAllByRole("link")).toHaveLength(11);
    expect(screen.getByTestId("nav-ui-talad-010").getAttribute("href")).toBe("/stock");
  });

  it("marks the page being viewed and shows who is signed in", () => {
    nav.pathname = "/members";
    render(<AppFrame me={cashier}>page</AppFrame>);

    expect(screen.getByTestId("nav-ui-talad-004").getAttribute("aria-current")).toBe("page");
    expect(screen.getByTestId("nav-ui-talad-002").getAttribute("aria-current")).toBeNull();
    expect(screen.getByText("สมชาย")).toBeTruthy();
  });
});

describe("FE-talad-004 · AC-talad-045 · a screen the role may not open", () => {
  it("sends a cashier opening the stock screen to the sales page with the notice flag", async () => {
    vi.stubGlobal("fetch", apiMe(200, cashier));

    await expect(requireScreen("UI-talad-010")).rejects.toThrow("NEXT_REDIRECT /?denied=1");
  });

  it("lets a cashier open the sales page", async () => {
    vi.stubGlobal("fetch", apiMe(200, cashier));

    await expect(requireScreen("UI-talad-002")).resolves.toMatchObject({ username: "somchai" });
  });

  it("the sales page shows คุณไม่มีสิทธิ์เข้าถึงหน้านี้ after being sent back", () => {
    nav.search = new URLSearchParams("denied=1");
    render(<DeniedNotice />);

    expect(screen.getByRole("alert").textContent).toBe("คุณไม่มีสิทธิ์เข้าถึงหน้านี้");
  });

  it("shows no notice on an ordinary visit", () => {
    render(<DeniedNotice />);

    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("a token the api refuses ends the session instead of drawing anything", async () => {
    vi.stubGlobal("fetch", apiMe(401));

    await expect(requireScreen("UI-talad-002")).rejects.toThrow("NEXT_REDIRECT /logout");
  });
});

describe("FE-talad-004 · sign out (API-002)", () => {
  it("tells the api, clears the cookie and lands on sign-in", async () => {
    const fetchMock = apiMe(204);
    vi.stubGlobal("fetch", fetchMock);

    const response = await logout.POST(new Request("http://localhost/logout", { method: "POST" }) as never);

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringMatching(/\/api\/auth\/logout$/),
      expect.objectContaining({ method: "POST", headers: { authorization: "Bearer jwt-somchai" } }),
    );
    expect(jar.deleted).toEqual(["talad_session"]);
    expect(response.status).toBe(303);
    expect(response.headers.get("location")).toBe("http://localhost/login");
  });

  it("still clears the cookie when the api cannot be reached", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("fetch failed"); }));

    const response = await logout.GET(new Request("http://localhost/logout") as never);

    expect(jar.deleted).toEqual(["talad_session"]);
    expect(response.headers.get("location")).toBe("http://localhost/login");
  });

  it("is reachable with or without a session", () => {
    expect(decideRoute("/logout", "", false)).toEqual({ kind: "pass" });
    expect(decideRoute("/logout", "", true)).toEqual({ kind: "pass" });
  });
});
