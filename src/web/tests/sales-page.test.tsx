import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

const revalidatePath = vi.hoisted(() => vi.fn());
vi.mock("next/cache", () => ({ revalidatePath }));
vi.mock("next/headers", () => ({ cookies: async () => ({ get: () => ({ value: "jwt-somchai" }) }) }));

import { noPromotions } from "./pricing-fixture";
const { CartPanel } = await import("@/components/cart-panel");
const { ProductGrid } = await import("@/components/product-grid");
const { AddToCartButton } = await import("@/components/add-to-cart-button");
const actions = await import("@/app/(app)/cart-actions");
const api = await import("@/lib/sales-api");

beforeAll(() => {
  // jsdom draws <dialog> but does not implement showModal/close
  HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) { this.open = true; };
  HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) { this.open = false; };
});
beforeEach(() => revalidatePath.mockClear());
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

const orange = (qty: number) => ({ productId: 1, name: "ส้มสายน้ำผึ้ง", price: 45, qty, lineTotal: 45 * qty, stockQty: 10, lowStock: false });
const mangosteen = { productId: 2, name: "มังคุด แพ็ก", price: 120, qty: 1, lineTotal: 120, stockQty: 10, lowStock: false };
const cartOf = (...lines: ReturnType<typeof orange>[]) => ({ id: 7, status: "Open", lines, subtotal: lines.reduce((s, l) => s + l.lineTotal, 0) });
const ok = async () => ({ ok: true as const });

describe("FE-talad-006 · UI-talad-002 cart zone", () => {
  it("AC-talad-001 · + raises the line by one", async () => {
    const changeQty = vi.fn(ok);
    render(<CartPanel cart={cartOf(orange(1))} pricing={noPromotions(cartOf(orange(1)))} changeQty={changeQty} remove={vi.fn(ok)} />);

    await userEvent.click(screen.getByTestId("ui-talad-002-increase-qty"));

    expect(changeQty).toHaveBeenCalledWith(1, 2);
  });

  it("AC-talad-001 · shows qty 3 and total ฿135.00", () => {
    render(<CartPanel cart={cartOf(orange(3))} pricing={noPromotions(cartOf(orange(3)))} changeQty={vi.fn(ok)} remove={vi.fn(ok)} />);

    expect(screen.getByTestId("ui-talad-002-ent-010-qty")).toHaveProperty("value", "3");
    expect(screen.getByTestId("ui-talad-002-subtotal-after-item-promo").textContent).toBe("฿135.00");
  });

  it("AC-talad-002 · − from 3 lowers to 2 with no confirmation", async () => {
    const changeQty = vi.fn(ok);
    render(<CartPanel cart={cartOf(orange(3))} pricing={noPromotions(cartOf(orange(3)))} changeQty={changeQty} remove={vi.fn(ok)} />);

    await userEvent.click(screen.getByTestId("ui-talad-002-decrease-qty"));

    expect(changeQty).toHaveBeenCalledWith(1, 2);
    expect(screen.queryByText(/ต้องการลบ/)).toBeNull();
  });

  it("AC-talad-003 · − at 1 asks first, and ลบ takes the line out", async () => {
    const remove = vi.fn(ok);
    render(<CartPanel cart={cartOf(orange(1), mangosteen)} pricing={noPromotions(cartOf(orange(1), mangosteen))} changeQty={vi.fn(ok)} remove={remove} />);

    await userEvent.click(screen.getAllByTestId("ui-talad-002-decrease-qty")[0]);
    expect(screen.getByText("ต้องการลบ ส้มสายน้ำผึ้ง ออกจากตะกร้าหรือไม่?")).toBeTruthy();
    await userEvent.click(screen.getByRole("button", { name: "ลบ" }));

    expect(remove).toHaveBeenCalledWith(1);
  });

  it("AC-talad-003 · ยกเลิก keeps the line", async () => {
    const remove = vi.fn(ok);
    render(<CartPanel cart={cartOf(orange(1))} pricing={noPromotions(cartOf(orange(1)))} changeQty={vi.fn(ok)} remove={remove} />);

    await userEvent.click(screen.getByTestId("ui-talad-002-remove-line"));
    await userEvent.click(screen.getByRole("button", { name: "ยกเลิก" }));

    expect(remove).not.toHaveBeenCalled();
  });

  it("AC-talad-069 · a refused + shows the rule's sentence", async () => {
    const changeQty = vi.fn(async () => ({ ok: false as const, message: "ส้มสายน้ำผึ้ง คงเหลือไม่พอ (เหลือ 2)" }));
    render(<CartPanel cart={cartOf(orange(2))} pricing={noPromotions(cartOf(orange(2)))} changeQty={changeQty} remove={vi.fn(ok)} />);

    await userEvent.click(screen.getByTestId("ui-talad-002-increase-qty"));

    expect((await screen.findByRole("alert")).textContent).toBe("ส้มสายน้ำผึ้ง คงเหลือไม่พอ (เหลือ 2)");
  });

  it("AC-talad-049 · a cart line has no price to edit", () => {
    render(<CartPanel cart={cartOf(orange(1))} pricing={noPromotions(cartOf(orange(1)))} changeQty={vi.fn(ok)} remove={vi.fn(ok)} />);

    expect(screen.getAllByRole("textbox")).toHaveLength(1); // the read-only quantity only
    expect(screen.queryByTestId("ui-talad-002-edit-price")).toBeNull();
  });

  it("an empty cart says so", () => {
    render(<CartPanel cart={cartOf()} pricing={noPromotions(cartOf())} changeQty={vi.fn(ok)} remove={vi.fn(ok)} />);

    expect(screen.getByText("ยังไม่มีสินค้าในตะกร้า")).toBeTruthy();
  });
});

describe("FE-talad-006 · UI-talad-002 product browser", () => {
  const card = (id: number, name: string, stockQty: number, lowStock: boolean) => ({ id, name, barcode: null, price: 45, stockQty, lowStock, hasImage: false });

  it("AC-talad-071 / 072 · the ใกล้หมด badge is on the low card only", () => {
    render(
      <ProductGrid
        page={{ items: [card(1, "ส้มสายน้ำผึ้ง", 5, true), card(2, "มังคุด แพ็ก", 6, false)], page: 1, pageSize: 20, total: 2 }}
        search=""
        addToCart={vi.fn(ok)}
      />,
    );

    expect(screen.getAllByTestId("ui-talad-002-ent-001-low-stock-threshold")).toHaveLength(1);
    expect(screen.getAllByTestId("ui-talad-002-ent-001-name").map((e) => e.textContent)).toEqual(["ส้มสายน้ำผึ้ง", "มังคุด แพ็ก"]);
    expect(screen.getAllByTestId("ui-talad-002-ent-002-price")[0].textContent).toBe("฿45.00");
  });

  it("a search that finds nothing says ไม่พบสินค้า", () => {
    render(<ProductGrid page={{ items: [], page: 1, pageSize: 20, total: 0 }} search="xyz" addToCart={vi.fn(ok)} />);

    expect(screen.getByText("ไม่พบสินค้า")).toBeTruthy();
  });

  it("many products are paged by the server", () => {
    render(<ProductGrid page={{ items: [card(1, "ส้ม", 9, false)], page: 1, pageSize: 20, total: 45 }} search="ส้ม" addToCart={vi.fn(ok)} />);

    expect(screen.getByText("หน้า 1 / 3")).toBeTruthy();
    expect(screen.getByRole("link", { name: "ถัดไป ›" }).getAttribute("href")).toBe("/?search=%E0%B8%AA%E0%B9%89%E0%B8%A1&page=2");
  });

  it("AC-talad-110 · a product with nothing left answers on its card", async () => {
    const action = vi.fn(async () => ({ ok: false as const, message: "มังคุด แพ็ก คงเหลือไม่พอ (เหลือ 0)" }));
    render(<AddToCartButton productId={2} action={action} />);

    await userEvent.click(screen.getByTestId("ui-talad-002-add-to-cart"));

    expect((await screen.findByRole("alert")).textContent).toBe("มังคุด แพ็ก คงเหลือไม่พอ (เหลือ 0)");
    expect(action).toHaveBeenCalledWith(2);
  });
});

describe("FE-talad-006 · cart actions → API-005..007", () => {
  const answer = (status: number, body: unknown) =>
    vi.fn(async () => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } }));

  it("AC-talad-001 · add-to-cart posts the product with the session's JWT and refreshes the page", async () => {
    const fetchMock = answer(200, cartOf(orange(1)));
    vi.stubGlobal("fetch", fetchMock);

    expect(await actions.addToCart(1)).toEqual({ ok: true });

    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toMatch(/\/api\/cart\/lines$/);
    expect(init.method).toBe("POST");
    expect(init.body).toBe(JSON.stringify({ productId: 1 }));
    expect(new Headers(init.headers).get("authorization")).toBe("Bearer jwt-somchai");
    expect(revalidatePath).toHaveBeenCalledWith("/");
  });

  it("AC-talad-069 · a 409 comes back as the api's sentence and nothing is refreshed", async () => {
    vi.stubGlobal("fetch", answer(409, { code: "INSUFFICIENT_STOCK", message: "ส้มสายน้ำผึ้ง คงเหลือไม่พอ (เหลือ 2)" }));

    expect(await actions.changeQty(1, 3)).toEqual({ ok: false, message: "ส้มสายน้ำผึ้ง คงเหลือไม่พอ (เหลือ 2)" });
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it("AC-talad-003 · remove sends DELETE for that product", async () => {
    const fetchMock = answer(200, cartOf(mangosteen));
    vi.stubGlobal("fetch", fetchMock);

    await actions.removeFromCart(1);

    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toMatch(/\/api\/cart\/lines\/1$/);
    expect(init.method).toBe("DELETE");
  });

  it("an api nobody can reach asks to try again and leaves the cart", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("fetch failed"); }));

    const result = await actions.addToCart(1);

    expect(result.ok).toBe(false);
    expect(result.ok ? "" : result.message).toContain("ลองใหม่");
  });

  it("the product search asks API-003 for that page and term", async () => {
    const fetchMock = answer(200, { items: [], page: 2, pageSize: 20, total: 0 });
    vi.stubGlobal("fetch", fetchMock);

    await api.searchProducts("ส้ม", 2);

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    expect(String((fetchMock.mock.calls[0] as unknown as [string])[0])).toMatch(/\/api\/products\?page=2&search=%E0%B8%AA%E0%B9%89%E0%B8%A1$/);
  });
});
