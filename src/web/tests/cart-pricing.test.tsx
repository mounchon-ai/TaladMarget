import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { Cart, CartPricing, PricedLine } from "@/lib/sales-api";

vi.mock("next/cache", () => ({ revalidatePath: vi.fn() }));
vi.mock("next/headers", () => ({ cookies: async () => ({ get: () => ({ value: "jwt-somchai" }) }) }));
vi.mock("@/lib/me", () => ({ requireScreen: async () => ({ username: "somchai" }) }));

const { CartPanel } = await import("@/components/cart-panel");
const { PromotionChoiceDialog } = await import("@/components/promotion-choice-dialog");
const { default: SalesPage } = await import("@/app/(app)/page");
const api = await import("@/lib/sales-api");

// AC-talad-091 · 065: ส้มสายน้ำผึ้ง ×2 at 45 under "ส้มสายน้ำผึ้ง ลด 10%" (9 off) and มังคุด แพ็ก ×1 at 120
const cartLine = (productId: number, name: string, price: number, qty: number) => ({ productId, name, price, qty, lineTotal: price * qty, stockQty: 10, lowStock: false });
const cart: Cart = { id: 7, status: "Open", lines: [cartLine(1, "ส้มสายน้ำผึ้ง", 45, 2), cartLine(2, "มังคุด แพ็ก", 120, 1)], subtotal: 210 };
const priced = (productId: number, name: string, qty: number, unitPrice: number, promotion: string | null, discount: number | null): PricedLine => ({
  productId,
  name,
  qty,
  unitPrice,
  lineGross: unitPrice * qty,
  promotion: promotion ? { id: productId * 10, name: promotion } : null,
  itemPromoDiscount: discount,
  lineNet: discount == null ? null : unitPrice * qty - discount,
});
const pricing = (over: Partial<CartPricing>): CartPricing => ({
  lines: [priced(1, "ส้มสายน้ำผึ้ง", 2, 45, "ส้มสายน้ำผึ้ง ลด 10%", 9), priced(2, "มังคุด แพ็ก", 1, 120, null, 0)],
  promoDiscount: 9,
  subtotal: 201,
  billPromotion: { id: 5, name: "ลดทั้งบิล 10%" },
  billRate: 10,
  billDiscount: 20.1,
  afterBill: 180.9,
  member: null,
  memberRate: 5,
  memberDiscount: 9.05,
  net: 171.85,
  needsChoice: null,
  ...over,
});
const ok = async () => ({ ok: true as const });
const text = (testid: string) => screen.getByTestId(testid).textContent;

beforeEach(() => {
  // jsdom draws <dialog> but does not implement showModal/close
  HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) { this.open = true; };
  HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) { this.open = false; };
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FE-talad-032 · API-009 on the web", () => {
  it("asks for the caller's own cart with the seller's token, and each choice in order", async () => {
    const fetch = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(async () => new Response(JSON.stringify(pricing({})), { status: 200 }));
    vi.stubGlobal("fetch", fetch);

    await api.getCartPricing();
    await api.getCartPricing([7, 9]);

    const [first, second] = fetch.mock.calls.map(([u]) => new URL(u));
    expect([first.pathname, first.search]).toEqual(["/api/cart/pricing", ""]);
    expect(second.searchParams.getAll("choice")).toEqual(["7", "9"]);
    expect(new Headers(fetch.mock.calls[0][1]?.headers).get("authorization")).toBe("Bearer jwt-somchai");
  });

  it("the sales page reads the cart and its pricing and draws the totals", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (url: string) => {
        const path = new URL(url).pathname;
        const body = path === "/api/cart/pricing" ? pricing({}) : path === "/api/cart" ? cart : { items: [], page: 1, pageSize: 20, total: 0 };
        return new Response(JSON.stringify(body), { status: 200 });
      }),
    );

    render(await SalesPage({ searchParams: Promise.resolve({}) }));

    expect(text("ui-talad-002-amount-due")).toBe("฿171.85");
  });

  it("state error · pricing that cannot be read shows — and says so; the cart is still there", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (url: string) => {
        const path = new URL(url).pathname;
        if (path === "/api/cart/pricing") return new Response(null, { status: 500 });
        return new Response(JSON.stringify(path === "/api/cart" ? cart : { items: [], page: 1, pageSize: 20, total: 0 }), { status: 200 });
      }),
    );

    render(await SalesPage({ searchParams: Promise.resolve({}) }));

    expect(["ui-talad-002-subtotal-after-item-promo", "ui-talad-002-bill-discount", "ui-talad-002-member-discount", "ui-talad-002-amount-due"].map(text)).toEqual(["—", "—", "—", "—"]);
    expect(screen.getByRole("alert").textContent).toBe("เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่");
    expect(screen.getAllByTestId("ui-talad-002-ent-010-product")).toHaveLength(2);
  });
});

describe("FE-talad-032 · UI-talad-002 zone totals", () => {
  it("AC-talad-091 · item promotion, whole bill, member — one after the other — and 171.85 to pay", () => {
    render(<CartPanel cart={cart} pricing={pricing({})} changeQty={vi.fn(ok)} remove={vi.fn(ok)} />);

    expect(text("ui-talad-002-subtotal-after-item-promo")).toBe("฿201.00");
    expect(text("ui-talad-002-bill-discount")).toBe("−฿20.10");
    expect(text("ui-talad-002-member-discount")).toBe("−฿9.05");
    expect(text("ui-talad-002-amount-due")).toBe("฿171.85");
  });

  it("AC-talad-125 · the promotion that took a line is named under it with what it gave; a line no promotion took has no tag", () => {
    render(<CartPanel cart={cart} pricing={pricing({})} changeQty={vi.fn(ok)} remove={vi.fn(ok)} />);

    const rows = screen.getAllByTestId("ui-talad-002-ent-010-product").map((p) => p.closest("[data-row-key]") as HTMLElement);
    expect(within(rows[0]).getByTestId("ui-talad-002-ent-006-name").textContent).toBe("ส้มสายน้ำผึ้ง ลด 10%");
    expect(within(rows[0]).getByText("−฿9.00")).toBeTruthy();
    expect(within(rows[1]).queryByTestId("ui-talad-002-ent-006-name")).toBeNull();
  });

  it("AC-talad-128 · a set promotion shows its part under each of its lines", () => {
    const set = "ลำไย 1 + ส้มสายน้ำผึ้ง 1 ลด 11%";
    const both: Cart = { id: 7, status: "Open", lines: [cartLine(3, "ลำไย", 19.75, 1), cartLine(1, "ส้มสายน้ำผึ้ง", 45, 1)], subtotal: 64.75 };
    const setPricing = pricing({
      lines: [priced(3, "ลำไย", 1, 19.75, set, 2.17), priced(1, "ส้มสายน้ำผึ้ง", 1, 45, set, 4.95)].map((l) => ({ ...l, promotion: { id: 44, name: set } })),
      promoDiscount: 7.12, subtotal: 57.63, billPromotion: null, billRate: 0, billDiscount: 0, afterBill: 57.63, memberRate: 0, memberDiscount: 0, net: 57.63,
    });

    render(<CartPanel cart={both} pricing={setPricing} changeQty={vi.fn(ok)} remove={vi.fn(ok)} />);

    expect(screen.getAllByTestId("ui-talad-002-ent-006-name").map((t) => t.textContent)).toEqual([set, set]);
    expect([screen.getByText("−฿2.17"), screen.getByText("−฿4.95")]).toHaveLength(2);
    expect([text("ui-talad-002-bill-discount"), text("ui-talad-002-member-discount"), text("ui-talad-002-amount-due")]).toEqual(["฿0.00", "฿0.00", "฿57.63"]);
  });

  it("AC-talad-092 · a 100% member discount leaves 0.00 to pay", () => {
    const one: Cart = { id: 7, status: "Open", lines: [cartLine(1, "ส้มสายน้ำผึ้ง", 45, 1)], subtotal: 45 };
    render(
      <CartPanel
        cart={one}
        pricing={pricing({ lines: [priced(1, "ส้มสายน้ำผึ้ง", 1, 45, null, 0)], promoDiscount: 0, subtotal: 45, billPromotion: null, billRate: 0, billDiscount: 0, afterBill: 45, memberRate: 100, memberDiscount: 45, net: 0 })}
        changeQty={vi.fn(ok)}
        remove={vi.fn(ok)}
      />,
    );

    expect([text("ui-talad-002-member-discount"), text("ui-talad-002-amount-due")]).toEqual(["−฿45.00", "฿0.00"]);
  });

  it("AC-talad-127 · while promotions give the same and wait for the staff, no total and no promotion is shown", () => {
    const tie = pricing({
      lines: [priced(1, "ส้มสายน้ำผึ้ง", 3, 45, null, null)],
      promoDiscount: null, subtotal: null, billPromotion: null, billRate: null, billDiscount: null, afterBill: null, memberRate: null, memberDiscount: null, net: null,
      needsChoice: [{ id: 7, name: "ส้มสายน้ำผึ้ง ลด 10%", discount: 13.5 }, { id: 9, name: "ซื้อ ส้มสายน้ำผึ้ง 3 ชิ้น ลด 10%", discount: 13.5 }],
    });

    render(<CartPanel cart={{ id: 7, status: "Open", lines: [cartLine(1, "ส้มสายน้ำผึ้ง", 45, 3)], subtotal: 135 }} pricing={tie} changeQty={vi.fn(ok)} remove={vi.fn(ok)} />);

    expect(["ui-talad-002-subtotal-after-item-promo", "ui-talad-002-bill-discount", "ui-talad-002-member-discount", "ui-talad-002-amount-due"].map(text)).toEqual(["—", "—", "—", "—"]);
    expect(screen.queryByTestId("ui-talad-002-ent-006-name")).toBeNull();
    expect(screen.queryByRole("alert")).toBeNull();
  });
});

describe("FE-talad-032 · UI-talad-003 window", () => {
  const choices = [
    { id: 7, name: "ส้มสายน้ำผึ้ง ลด 10%", discount: 13.5 },
    { id: 9, name: "ซื้อ ส้มสายน้ำผึ้ง 3 ชิ้น ลด 10%", discount: 13.5 },
  ];

  it("AC-talad-127 · lists each tied promotion keyed by its id, with the baht it gives, and the question", () => {
    render(<PromotionChoiceDialog choices={choices} open onChoose={vi.fn()} onCancel={vi.fn()} />);

    const dialog = document.querySelector("dialog")!;
    expect(dialog.open).toBe(true);
    expect(dialog.textContent).toContain("มีโปรโมชั่นที่ลดเท่ากัน กรุณาเลือกโปรโมชั่น");
    const names = within(dialog).getAllByTestId("ui-talad-003-ent-006-name");
    expect(names.map((n) => [n.closest("tr")?.getAttribute("data-row-key"), n.textContent])).toEqual([
      ["7", "ส้มสายน้ำผึ้ง ลด 10%"],
      ["9", "ซื้อ ส้มสายน้ำผึ้ง 3 ชิ้น ลด 10%"],
    ]);
    expect(within(dialog).getAllByTestId("ui-talad-003-promo-discount").map((d) => d.textContent)).toEqual(["−฿13.50", "−฿13.50"]);
  });

  it("เลือกโปรนี้ hands over that promotion; ยกเลิก leaves everything as it was", async () => {
    const onChoose = vi.fn();
    const onCancel = vi.fn();
    render(<PromotionChoiceDialog choices={choices} open onChoose={onChoose} onCancel={onCancel} />);

    await userEvent.click(within(screen.getAllByTestId("ui-talad-003-ent-006-name")[0].closest("tr")!).getByTestId("ui-talad-003-choose-promo"));
    await userEvent.click(screen.getByTestId("ui-talad-003-cancel"));

    expect(onChoose).toHaveBeenCalledExactlyOnceWith(7);
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it("state loading · the choose buttons are off while paying; closed when not asked", () => {
    const { rerender } = render(<PromotionChoiceDialog choices={choices} open pending onChoose={vi.fn()} onCancel={vi.fn()} />);

    expect(screen.getAllByTestId("ui-talad-003-choose-promo").every((b) => (b as HTMLButtonElement).disabled)).toBe(true);

    rerender(<PromotionChoiceDialog choices={choices} open={false} onChoose={vi.fn()} onCancel={vi.fn()} />);
    expect(document.querySelector("dialog")!.open).toBe(false);
  });
});
