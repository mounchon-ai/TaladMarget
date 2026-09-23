import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { ProductCard } from "@/lib/sales-api";

const requireScreen = vi.hoisted(() => vi.fn<(screen: string) => Promise<{ username: string }>>(async () => ({ username: "owner" })));
vi.mock("next/headers", () => ({ cookies: async () => ({ get: () => ({ value: "jwt-owner" }) }) }));
vi.mock("next/navigation", () => ({ redirect: (to: string) => { throw new Error(`NEXT_REDIRECT ${to}`); } }));
vi.mock("@/lib/me", () => ({ requireScreen: (s: string) => requireScreen(s) }));

const { default: StockPage } = await import("@/app/(app)/stock/page");
const { StockList } = await import("@/components/stock-list");

// AC-talad-073: the same 3 left, thresholds 5 and 2 — the api says which one is low
const orange: ProductCard = { id: 1, name: "ส้มสายน้ำผึ้ง", barcode: "8850000000011", price: 45, stockQty: 3, lowStock: true, hasImage: false };
const mangosteen: ProductCard = { id: 5, name: "มังคุด แพ็ก", barcode: null, price: 120, stockQty: 3, lowStock: false, hasImage: false };
const pageOf = (items: ProductCard[], total = items.length, page = 1) => ({ items, page, pageSize: 20, total });

function apiAnswers(status: number, body: unknown) {
  return vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(
    async () => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } }),
  );
}

beforeEach(() => requireScreen.mockClear());
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FE-talad-018 · UI-talad-010 stock page (API-003)", () => {
  it("AC-talad-045 · asks for its own screen first, then searches with the owner's token, the term and page from the URL", async () => {
    const fetch = apiAnswers(200, pageOf([orange]));
    vi.stubGlobal("fetch", fetch);

    await StockPage({ searchParams: Promise.resolve({ q: "8850000000011", page: "2" }) });

    expect(requireScreen).toHaveBeenCalledWith("UI-talad-010");
    await waitFor(() => expect(fetch).toHaveBeenCalled());
    const [url, init] = fetch.mock.calls[0];
    const u = new URL(url);
    expect([u.pathname, u.searchParams.get("search"), u.searchParams.get("page")]).toEqual(["/api/products", "8850000000011", "2"]);
    expect(new Headers(init?.headers).get("authorization")).toBe("Bearer jwt-owner");
  });

  it("state unauthorized · a seller is sent away before any product is read", async () => {
    const fetch = apiAnswers(200, pageOf([orange]));
    vi.stubGlobal("fetch", fetch);
    requireScreen.mockRejectedValueOnce(new Error("NEXT_REDIRECT /?denied=1"));

    await expect(StockPage({ searchParams: Promise.resolve({}) })).rejects.toThrow("NEXT_REDIRECT /?denied=1");
    expect(fetch).not.toHaveBeenCalled();
  });

  it("the search box keeps the term the page was opened with", async () => {
    vi.stubGlobal("fetch", apiAnswers(200, pageOf([])));

    render(await StockPage({ searchParams: Promise.resolve({ q: "ส้ม" }) }));

    expect((screen.getByTestId("ui-talad-010-search-text") as HTMLInputElement).value).toBe("ส้ม");
    expect(screen.getByTestId("ui-talad-010-search").textContent).toBe("ค้นหา");
  });
});

describe("FE-talad-018 · UI-talad-010 results", () => {
  it("AC-talad-073 · same stock, different thresholds: ใกล้หมด on ส้มสายน้ำผึ้ง only", async () => {
    render(await StockList({ loaded: Promise.resolve({ ok: true, page: pageOf([mangosteen, orange]) }), q: "" }));

    const badges = screen.getAllByTestId("ui-talad-010-ent-001-low-stock-threshold");
    expect(badges.map((b) => [b.closest("tr")?.getAttribute("data-row-key"), b.textContent])).toEqual([
      ["5", ""],
      ["1", "ใกล้หมด"],
    ]);
    expect(screen.getAllByTestId("ui-talad-010-ent-001-stock-qty").map((c) => c.textContent)).toEqual(["3", "3"]);
  });

  it.each([
    ["AC-talad-071", 5, true],
    ["AC-talad-072", 6, false],
    ["AC-talad-074", 14, false],
  ])("%s · %i left shows the badge as the api flags it", async (_ac, left, low) => {
    render(await StockList({ loaded: Promise.resolve({ ok: true, page: pageOf([{ ...orange, stockQty: left, lowStock: low }]) }), q: "" }));

    expect(screen.getByTestId("ui-talad-010-ent-001-stock-qty").textContent).toBe(String(left));
    expect(screen.getByTestId("ui-talad-010-ent-001-low-stock-threshold").textContent).toBe(low ? "ใกล้หมด" : "");
  });

  it("each row keyed by the product's id with name, barcode (— when none) and price in baht", async () => {
    render(await StockList({ loaded: Promise.resolve({ ok: true, page: pageOf([mangosteen, orange]) }), q: "" }));

    const names = screen.getAllByTestId("ui-talad-010-ent-001-name");
    expect(names.map((n) => [n.closest("tr")?.getAttribute("data-row-key"), n.textContent, n.getAttribute("title")])).toEqual([
      ["5", "มังคุด แพ็ก", "มังคุด แพ็ก"],
      ["1", "ส้มสายน้ำผึ้ง", "ส้มสายน้ำผึ้ง"],
    ]);
    expect(screen.getAllByTestId("ui-talad-010-ent-001-barcode").map((c) => c.textContent)).toEqual(["—", "8850000000011"]);
    expect(screen.getAllByTestId("ui-talad-010-ent-002-price").map((c) => c.textContent)).toEqual(["฿120.00", "฿45.00"]);
  });

  it("state empty · ไม่พบสินค้า", async () => {
    render(await StockList({ loaded: Promise.resolve({ ok: true, page: pageOf([]) }), q: "ไม่มี" }));

    expect(screen.getByText("ไม่พบสินค้า")).toBeTruthy();
    expect(screen.queryByRole("table")).toBeNull();
  });

  it("state error · โหลดข้อมูลไม่สำเร็จ with a retry that keeps the term", async () => {
    render(await StockList({ loaded: Promise.resolve({ ok: false }), q: "ส้ม" }));

    expect(screen.getByRole("alert").textContent).toContain("โหลดข้อมูลไม่สำเร็จ");
    expect(screen.getByText("ลองใหม่").getAttribute("href")).toBe(`/stock?q=${encodeURIComponent("ส้ม")}`);
  });

  it("state overflow · 21 products page at 20, with a link to the next page that keeps the term", async () => {
    const twenty = Array.from({ length: 20 }, (_, i) => ({ ...orange, id: 100 + i }));
    render(await StockList({ loaded: Promise.resolve({ ok: true, page: pageOf(twenty, 21) }), q: "ส้ม" }));

    expect(screen.getAllByTestId("ui-talad-010-ent-001-name")).toHaveLength(20);
    expect(screen.getByText("แสดง 1–20 จาก 21 แถว")).toBeTruthy();
    expect(screen.getByText("ถัดไป ›").getAttribute("href")).toBe(`/stock?q=${encodeURIComponent("ส้ม")}&page=2`);
  });
});
