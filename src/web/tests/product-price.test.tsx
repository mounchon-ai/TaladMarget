import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import type { PriceChange, ProductDetail } from "@/lib/products-api";
import type { ProductCard } from "@/lib/sales-api";

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

const { repriceAction } = await import("@/app/(app)/stock/[id]/actions");
const { default: ProductPage } = await import("@/app/(app)/stock/[id]/page");
const { default: StockPage } = await import("@/app/(app)/stock/page");
const { PriceHistory } = await import("@/components/price-history");
const { EditPriceDialog } = await import("@/components/edit-price-dialog");
const { StockList } = await import("@/components/stock-list");

const PRICE_INVALID = "ราคาต้องมากกว่า 0 และมีทศนิยมไม่เกิน 2 ตำแหน่ง";

// AC-talad-084: 45 → 50 at 10:00, then 50 → 55 at 10:30 (23 ก.ย. 2569, Thai time), newest first
const first: PriceChange = { id: 6, previousPrice: 45, price: 50, source: "STOCK_SCREEN", changedById: 1, changedByName: "เจ้าของร้าน", changedAt: "2026-09-23T03:00:00Z" };
const second: PriceChange = { ...first, id: 7, previousPrice: 50, price: 55, changedAt: "2026-09-23T03:30:00Z" };
const historyOf = (items: PriceChange[], total = items.length, page = 1) => ({ items, page, pageSize: 20, total });
const orange: ProductDetail = {
  id: 1,
  name: "ส้มสายน้ำผึ้ง",
  barcode: "8850000000011",
  stockQty: 3,
  lowStockThreshold: 5,
  lowStock: true,
  price: 45,
  hasImage: false,
  priceHistory: historyOf([]),
};

function apiAnswers(status: number, body: unknown) {
  return vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(
    async () => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } }),
  );
}

const priced = (price: string) => {
  const fd = new FormData();
  fd.set("price", price);
  return fd;
};

beforeAll(() => {
  // jsdom draws <dialog> but does not implement showModal/close
  HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) { this.open = true; };
  HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) { this.open = false; };
});
beforeEach(() => {
  revalidatePath.mockClear();
  redirect.mockClear();
  requireScreen.mockClear();
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FE-talad-020 · UI-talad-013 save action (API-022)", () => {
  it("AC-talad-082 · 50 goes to the api as a number from the stock screen with the owner's token; the detail is redrawn", async () => {
    const fetch = apiAnswers(201, { ...orange, price: 50, priceHistory: historyOf([first]) });
    vi.stubGlobal("fetch", fetch);

    expect(await repriceAction(1, undefined, priced("50"))).toEqual({ typed: "", saved: true });

    const [url, init] = fetch.mock.calls[0];
    expect([new URL(url).pathname, init?.method]).toEqual(["/api/products/1/prices", "POST"]);
    expect(JSON.parse(String(init?.body))).toEqual({ price: 50, source: "STOCK_SCREEN" });
    expect(new Headers(init?.headers).get("authorization")).toBe("Bearer jwt-owner");
    expect(revalidatePath).toHaveBeenCalledWith("/stock/1");
  });

  it.each([["0", 0], ["45.555", 45.555], ["", null], ["abc", null]])(
    "state error · %s goes as typed; the domain's sentence comes back under the field and nothing is redrawn",
    async (typed, sent) => {
      const fetch = apiAnswers(400, { code: "PRICE_INVALID", errors: [{ field: "price", message: PRICE_INVALID }] });
      vi.stubGlobal("fetch", fetch);

      expect(await repriceAction(1, undefined, priced(typed))).toEqual({ typed, error: PRICE_INVALID });
      expect(JSON.parse(String(fetch.mock.calls[0][1]?.body))).toEqual({ price: sent, source: "STOCK_SCREEN" });
      expect(revalidatePath).not.toHaveBeenCalled();
    },
  );

  it("state error · a product discontinued while the window was open: back to the stock list saying ไม่พบสินค้า", async () => {
    vi.stubGlobal("fetch", apiAnswers(404, { code: "PRODUCT_NOT_FOUND", errors: [] }));

    await expect(repriceAction(1, undefined, priced("50"))).rejects.toThrow("NEXT_REDIRECT /stock?gone=1");
  });

  it("a seller refused by the api gets the no-permission sentence; an expired session signs out; no server asks to try again", async () => {
    vi.stubGlobal("fetch", apiAnswers(403, {}));
    expect(await repriceAction(1, undefined, priced("50"))).toEqual({ typed: "50", message: "คุณไม่มีสิทธิ์เข้าถึงหน้านี้" });

    vi.stubGlobal("fetch", apiAnswers(401, {}));
    await expect(repriceAction(1, undefined, priced("50"))).rejects.toThrow("NEXT_REDIRECT /logout");

    vi.stubGlobal("fetch", vi.fn(async () => Promise.reject(new Error("ECONNREFUSED"))));
    expect(await repriceAction(1, undefined, priced("50"))).toEqual({ typed: "50", message: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่" });
  });
});

describe("FE-talad-020 · UI-talad-012 product page (API-019)", () => {
  it("AC-talad-045 · asks for its own screen first, then reads the product with its history page", async () => {
    const fetch = apiAnswers(200, orange);
    vi.stubGlobal("fetch", fetch);

    render(await ProductPage({ params: Promise.resolve({ id: "1" }), searchParams: Promise.resolve({ historyPage: "2" }) }));

    expect(requireScreen).toHaveBeenCalledWith("UI-talad-012");
    const url = new URL(fetch.mock.calls[0][0]);
    expect([url.pathname, url.searchParams.get("historyPage")]).toEqual(["/api/products/1", "2"]);
    expect(screen.getByTestId("ui-talad-012-ent-001-name").textContent).toBe("ส้มสายน้ำผึ้ง");
    expect(screen.getByTestId("ui-talad-012-ent-001-stock-qty").textContent).toBe("3");
    expect(screen.getByTestId("ui-talad-012-ent-001-low-stock-threshold").textContent).toBe("5");
    expect(screen.getByTestId("ui-talad-012-ent-002-price").textContent).toBe("฿45.00");
    expect(screen.getByTestId("ui-talad-012-back").getAttribute("href")).toBe("/stock");
  });

  it("state empty · no change of price yet: ยังไม่มีรายการ in the history section", async () => {
    vi.stubGlobal("fetch", apiAnswers(200, orange));

    render(await ProductPage({ params: Promise.resolve({ id: "1" }), searchParams: Promise.resolve({}) }));

    expect(screen.getByText("ยังไม่มีรายการ")).toBeTruthy();
  });

  it("state unauthorized · a seller is sent away before the product is read", async () => {
    const fetch = apiAnswers(200, orange);
    vi.stubGlobal("fetch", fetch);
    requireScreen.mockRejectedValueOnce(new Error("NEXT_REDIRECT /?denied=1"));

    await expect(ProductPage({ params: Promise.resolve({ id: "1" }), searchParams: Promise.resolve({}) })).rejects.toThrow("NEXT_REDIRECT /?denied=1");
    expect(fetch).not.toHaveBeenCalled();
  });

  it("an unknown or discontinued product goes back to the stock list saying ไม่พบสินค้า", async () => {
    vi.stubGlobal("fetch", apiAnswers(404, { code: "PRODUCT_NOT_FOUND", errors: [] }));

    await expect(ProductPage({ params: Promise.resolve({ id: "4" }), searchParams: Promise.resolve({}) })).rejects.toThrow("NEXT_REDIRECT /stock?gone=1");

    vi.stubGlobal("fetch", apiAnswers(200, { items: [], page: 1, pageSize: 20, total: 0 }));
    render(await StockPage({ searchParams: Promise.resolve({ gone: "1" }) }));
    expect(screen.getByRole("alert").textContent).toBe("ไม่พบสินค้า");
  });
});

describe("FE-talad-020 · UI-talad-012 price history", () => {
  it("AC-talad-084 · both changes, newest first, each keyed by its version with the old price, who and when (Thai time)", () => {
    render(<PriceHistory history={historyOf([second, first])} productId={1} />);

    const previous = screen.getAllByTestId("ui-talad-012-ent-002-previous-price");
    expect(previous.map((c) => [c.closest("tr")?.getAttribute("data-row-key"), c.textContent])).toEqual([
      ["7", "฿50.00"],
      ["6", "฿45.00"],
    ]);
    expect(screen.getAllByTestId("ui-talad-012-ent-002-changed-by").map((c) => c.textContent)).toEqual(["เจ้าของร้าน", "เจ้าของร้าน"]);
    const at = screen.getAllByTestId("ui-talad-012-ent-002-changed-at").map((c) => c.textContent);
    expect(at[1]).toContain("10:00");
    expect(at[1]).toContain("2569");
    expect(at[0]).toContain("10:30");
  });

  it("state overflow · 21 changes page at 20 on historyPage", () => {
    const twenty = Array.from({ length: 20 }, (_, i) => ({ ...first, id: 100 + i }));
    render(<PriceHistory history={historyOf(twenty, 21)} productId={1} />);

    expect(screen.getAllByTestId("ui-talad-012-ent-002-previous-price")).toHaveLength(20);
    expect(screen.getByText("ถัดไป ›").getAttribute("href")).toBe("/stock/1?historyPage=2");
  });
});

describe("FE-talad-020 · UI-talad-013 price window", () => {
  it("แก้ราคา opens the window with the price in force; ยกเลิก closes it without saving", async () => {
    const action = vi.fn(async () => ({ typed: "", saved: true }));
    render(<EditPriceDialog current={45} action={action} />);

    await userEvent.click(screen.getByTestId("ui-talad-012-edit-price"));
    const dialog = document.querySelector("dialog")!;
    expect(dialog.open).toBe(true);
    expect(within(dialog).getByTestId("ui-talad-013-current-price").textContent).toBe("฿45.00");

    await userEvent.click(within(dialog).getByTestId("ui-talad-013-cancel"));
    expect(dialog.open).toBe(false);
    expect(action).not.toHaveBeenCalled();
  });

  it("AC-talad-082 · บันทึก sends what was typed and closes the window", async () => {
    const action = vi.fn(async (_prev: unknown, fd: FormData) => (fd.get("price") === "50" ? { typed: "", saved: true } : { typed: "?" }));
    render(<EditPriceDialog current={45} action={action} />);

    await userEvent.click(screen.getByTestId("ui-talad-012-edit-price"));
    await userEvent.type(screen.getByTestId("ui-talad-013-ent-002-price"), "50");
    await userEvent.click(screen.getByTestId("ui-talad-013-save"));

    await waitFor(() => expect(document.querySelector("dialog")!.open).toBe(false));
    expect(action).toHaveBeenCalledTimes(1);
  });

  it("state error · a refused price keeps the window open, the typed value, and the sentence under the field", async () => {
    render(<EditPriceDialog current={45} action={async (_prev, fd) => ({ typed: String(fd.get("price")), error: PRICE_INVALID })} />);

    await userEvent.click(screen.getByTestId("ui-talad-012-edit-price"));
    await userEvent.type(screen.getByTestId("ui-talad-013-ent-002-price"), "0");
    await userEvent.click(screen.getByTestId("ui-talad-013-save"));

    const message = await screen.findByText(PRICE_INVALID);
    expect(message.closest("label")?.contains(screen.getByTestId("ui-talad-013-ent-002-price"))).toBe(true);
    expect((screen.getByTestId("ui-talad-013-ent-002-price") as HTMLInputElement).value).toBe("0");
    expect(document.querySelector("dialog")!.open).toBe(true);
    expect(screen.getByTestId("ui-talad-013-save").closest("form")?.noValidate).toBe(true);
  });

  it("state loading · save is off while saving", async () => {
    let finish: () => void = () => {};
    const action = vi.fn(() => new Promise<{ typed: string; saved: boolean }>((resolve) => (finish = () => resolve({ typed: "", saved: true }))));
    render(<EditPriceDialog current={45} action={action} />);

    await userEvent.click(screen.getByTestId("ui-talad-012-edit-price"));
    await userEvent.click(screen.getByTestId("ui-talad-013-save"));

    expect((screen.getByTestId("ui-talad-013-save") as HTMLButtonElement).disabled).toBe(true);
    finish();
  });
});

describe("FE-talad-020 · UI-talad-010 ดูสินค้า", () => {
  it("UIC-001 · UIC-002 — an icon named ดูสินค้า in the first cell of each row opens that product's UI-talad-012", async () => {
    const card: ProductCard = { id: 1, name: "ส้มสายน้ำผึ้ง", barcode: null, price: 45, stockQty: 3, lowStock: true, hasImage: false };
    render(await StockList({ loaded: Promise.resolve({ ok: true, page: { items: [card], page: 1, pageSize: 20, total: 1 } }), q: "" }));

    const open = screen.getByTestId("ui-talad-010-open-product");
    expect([open.getAttribute("href"), open.getAttribute("aria-label")]).toEqual(["/stock/1", "ดูสินค้า"]);
    expect(open.closest("td")).toBe(open.closest("tr")?.firstElementChild);
    expect(open.closest("tr")?.getAttribute("data-row-key")).toBe("1");
  });
});
