import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const revalidatePath = vi.hoisted(() => vi.fn());
const redirect = vi.hoisted(() =>
  vi.fn((to: string) => {
    throw new Error(`NEXT_REDIRECT ${to}`);
  }),
);
const requireScreen = vi.hoisted(() => vi.fn<(screen: string) => Promise<{ username: string }>>(async () => ({ username: "somchai" })));
vi.mock("next/cache", () => ({ revalidatePath }));
vi.mock("next/headers", () => ({ cookies: async () => ({ get: () => ({ value: "jwt-somchai" }) }) }));
vi.mock("next/navigation", () => ({ redirect: (to: string) => redirect(to) }));
vi.mock("@/lib/me", () => ({ requireScreen: (s: string) => requireScreen(s) }));

const { findMembers, bindToCart } = await import("@/app/(app)/cart-actions");
const { MemberPanel } = await import("@/components/member-panel");
const { MemberList, HeaderRegister } = await import("@/components/member-list");
const { default: MembersPage } = await import("@/app/(app)/members/page");

const NOT_FOUND = "ไม่พบสมาชิก — สมัครสมาชิกใหม่?";
const somying = { id: 11, name: "สมหญิง ใจดี", phone: "0812345678", accumulatedAmount: 1000, status: "Active" };
const sommai = { id: 12, name: "สมหมาย รักดี", phone: "0898765432", accumulatedAmount: 0, status: "Active" };
const pageOf = (items: (typeof somying)[], total = items.length, page = 1) => ({ items, page, pageSize: 20, total });

function apiAnswers(status: number, body: unknown) {
  return vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(
    async () => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } }),
  );
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

describe("FE-talad-012 · UI-talad-002 member actions (API-012 · API-008)", () => {
  it("AC-talad-016 · the whole phone is searched with the session's token and สมหญิง comes back with 1,000", async () => {
    const fetch = apiAnswers(200, pageOf([somying]));
    vi.stubGlobal("fetch", fetch);

    const result = await findMembers(" 0812345678 ");

    const [url, init] = fetch.mock.calls[0];
    expect(new URL(url).pathname).toBe("/api/members");
    expect(new URL(url).searchParams.get("q")).toBe("0812345678");
    expect(new Headers(init?.headers).get("authorization")).toBe("Bearer jwt-somchai");
    expect(result).toEqual({ ok: true, members: [somying] });
  });

  it("AC-talad-016 · choosing สมหญิง binds her to the caller's own cart and redraws the sales page", async () => {
    const fetch = apiAnswers(200, { id: 7, status: "Open", lines: [], subtotal: 100, member: somying });
    vi.stubGlobal("fetch", fetch);

    const result = await bindToCart(11);

    const [url, init] = fetch.mock.calls[0];
    expect(new URL(url).pathname).toBe("/api/cart/member");
    expect(init?.method).toBe("PUT");
    expect(JSON.parse(String(init?.body))).toEqual({ memberId: 11 });
    expect(result).toEqual({ ok: true });
    expect(revalidatePath).toHaveBeenCalledWith("/");
  });

  it("AC-talad-012 · a member hidden since the search is answered with the declared sentence, never the api's technical one", async () => {
    vi.stubGlobal("fetch", apiAnswers(404, { code: "MEMBER_NOT_FOUND", message: "member 11 does not exist or is hidden" }));

    const result = await bindToCart(11);

    expect(result).toEqual({ ok: false, message: NOT_FOUND });
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it("a paid cart's refusal is the rule's own sentence", async () => {
    vi.stubGlobal("fetch", apiAnswers(409, { code: "CART_NOT_OPEN", message: "ตะกร้านี้ชำระเงินไปแล้ว" }));

    expect(await bindToCart(11)).toEqual({ ok: false, message: "ตะกร้านี้ชำระเงินไปแล้ว" });
  });

  it("an expired session goes to sign-out", async () => {
    vi.stubGlobal("fetch", apiAnswers(401, {}));

    await expect(bindToCart(11)).rejects.toThrow("NEXT_REDIRECT /logout");
  });

  it("a server nobody can reach asks to try again, for search and for bind", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("fetch failed"); }));

    expect((await findMembers("สมห")).ok).toBe(false);
    expect(await bindToCart(11)).toEqual({ ok: false, message: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่" });
  });
});

describe("FE-talad-012 · UI-talad-002 member zone", () => {
  const ok = async () => ({ ok: true as const });

  it("AC-talad-017 · part of a name lists both with name, whole phone and what they bought; เลือกสมาชิก binds สมหมาย", async () => {
    const find = vi.fn(async () => ({ ok: true as const, members: [somying, sommai] }));
    const bind = vi.fn(ok);
    render(<MemberPanel member={null} find={find} bind={bind} />);

    await userEvent.type(screen.getByTestId("ui-talad-002-member-search"), "สมห");
    await userEvent.click(screen.getByTestId("ui-talad-002-search-member"));

    const choose = await screen.findAllByTestId("ui-talad-002-bind-member");
    expect(find).toHaveBeenCalledWith("สมห");
    expect(choose).toHaveLength(2);
    expect(screen.getByText("0812345678 · ยอดซื้อสะสม ฿1,000.00")).toBeTruthy();
    const row = choose[1].closest("[data-row-key]");
    expect(row?.getAttribute("data-row-key")).toBe("12");
    expect(row?.textContent).toContain("สมหมาย รักดี");

    await userEvent.click(choose[1]);

    expect(bind).toHaveBeenCalledWith(12);
    // the term stays in the box
    expect((screen.getByTestId("ui-talad-002-member-search") as HTMLInputElement).value).toBe("สมห");
  });

  it.each([
    ["AC-talad-018", "0899999999"],
    ["AC-talad-019", "5678"],
    ["AC-talad-012", "0812345678"],
  ])("%s · a search that finds nobody says so, offers to register, and binds nobody", async (_ac, term) => {
    const bind = vi.fn(ok);
    render(<MemberPanel member={null} find={async () => ({ ok: true, members: [] })} bind={bind} />);

    await userEvent.type(screen.getByTestId("ui-talad-002-member-search"), term);
    await userEvent.click(screen.getByTestId("ui-talad-002-search-member"));

    expect(await screen.findByText(NOT_FOUND)).toBeTruthy();
    expect(screen.queryByTestId("ui-talad-002-bind-member")).toBeNull();
    expect(screen.queryByTestId("ui-talad-002-ent-009-member")).toBeNull();
    expect(screen.getByTestId("ui-talad-002-register-member").getAttribute("href")).toBe("/members/new");
    expect(bind).not.toHaveBeenCalled();
  });

  it("AC-talad-016 · the cart's bound member is shown in the member zone", () => {
    render(<MemberPanel member={somying} find={vi.fn()} bind={vi.fn()} />);

    expect(screen.getByTestId("ui-talad-002-ent-009-member").textContent).toBe("สมหญิง ใจดี");
  });

  it("AC-talad-012 · a refused bind shows its sentence", async () => {
    render(
      <MemberPanel member={null} find={async () => ({ ok: true, members: [somying] })} bind={async () => ({ ok: false, message: NOT_FOUND })} />,
    );
    await userEvent.type(screen.getByTestId("ui-talad-002-member-search"), "สมหญิง");
    await userEvent.click(screen.getByTestId("ui-talad-002-search-member"));

    await userEvent.click(await screen.findByTestId("ui-talad-002-bind-member"));

    await waitFor(() => expect(screen.getByRole("alert").textContent).toBe(NOT_FOUND));
  });
});

describe("FE-talad-012 · UI-talad-004 members page", () => {
  it("AC-talad-045 · asks for its own screen first, then searches with the term and page from the URL", async () => {
    const fetch = apiAnswers(200, pageOf([somying]));
    vi.stubGlobal("fetch", fetch);

    await MembersPage({ searchParams: Promise.resolve({ q: "สมห", page: "2" }) });

    expect(requireScreen).toHaveBeenCalledWith("UI-talad-004");
    await waitFor(() => expect(fetch).toHaveBeenCalled());
    const url = new URL(fetch.mock.calls[0][0]);
    expect([url.pathname, url.searchParams.get("q"), url.searchParams.get("page")]).toEqual(["/api/members", "สมห", "2"]);
  });

  it("AC-talad-016 · each row keyed by the member's id, whole phone, amount bought, and a view link to UI-talad-006", async () => {
    render(await MemberList({ loaded: Promise.resolve({ ok: true, page: pageOf([somying, sommai]) }), q: "" }));

    const names = screen.getAllByTestId("ui-talad-004-ent-004-name");
    expect(names.map((n) => n.closest("tr")?.getAttribute("data-row-key"))).toEqual(["11", "12"]);
    expect(names[0].getAttribute("title")).toBe("สมหญิง ใจดี");
    expect(screen.getAllByTestId("ui-talad-004-ent-004-phone")[0].textContent).toBe("0812345678");
    expect(screen.getAllByTestId("ui-talad-004-ent-004-accumulated-amount")[0].textContent).toBe("฿1,000.00");
    const view = screen.getAllByTestId("ui-talad-004-open-member")[0];
    expect([view.getAttribute("href"), view.getAttribute("aria-label")]).toEqual(["/members/11", "ดูข้อมูล"]);
    // the view action is the first cell of its row (UIC-001)
    expect(view.closest("td")).toBe(view.closest("tr")?.firstElementChild);
  });

  it("state empty · the declared sentence with one register button, and the header draws none", async () => {
    const loaded = Promise.resolve({ ok: true as const, page: pageOf([]) });
    render(
      <>
        {await HeaderRegister({ loaded })}
        {await MemberList({ loaded, q: "0899999999" })}
      </>,
    );

    expect(screen.getByText(NOT_FOUND)).toBeTruthy();
    expect(screen.getAllByTestId("ui-talad-004-register")).toHaveLength(1);
    expect(screen.getByTestId("ui-talad-004-register").getAttribute("href")).toBe("/members/new");
  });

  it("with rows the header carries the one register button", async () => {
    render(<>{await HeaderRegister({ loaded: Promise.resolve({ ok: true, page: pageOf([somying]) }) })}</>);

    expect(screen.getAllByTestId("ui-talad-004-register")).toHaveLength(1);
  });

  it("state error · โหลดข้อมูลไม่สำเร็จ with a retry that keeps the term", async () => {
    render(await MemberList({ loaded: Promise.resolve({ ok: false }), q: "สมห" }));

    expect(screen.getByRole("alert").textContent).toContain("โหลดข้อมูลไม่สำเร็จ");
    expect(screen.getByText("ลองใหม่").getAttribute("href")).toBe(`/members?q=${encodeURIComponent("สมห")}`);
  });

  it("state overflow · 21 members page at 20, with a link to the next page", async () => {
    const twenty = Array.from({ length: 20 }, (_, i) => ({ ...somying, id: 100 + i, phone: `08000000${String(i).padStart(2, "0")}` }));
    render(await MemberList({ loaded: Promise.resolve({ ok: true, page: pageOf(twenty, 21) }), q: "" }));

    expect(screen.getAllByTestId("ui-talad-004-ent-004-name")).toHaveLength(20);
    expect(screen.getByText("ถัดไป ›").getAttribute("href")).toBe("/members?page=2");
  });
});
