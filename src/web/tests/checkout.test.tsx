import { act, cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { PromotionChoice } from "@/lib/sales-api";

const cache = vi.hoisted(() => ({ revalidatePath: vi.fn() }));
vi.mock("next/cache", () => cache);
vi.mock("next/headers", () => ({ cookies: async () => ({ get: () => ({ value: "jwt-somchai" }) }) }));
vi.mock("next/navigation", () => ({
  redirect: (to: string) => {
    throw new Error(`NEXT_REDIRECT ${to}`);
  },
}));
vi.mock("@/lib/me", () => ({ requireScreen: async () => ({ username: "somchai" }) }));

import { noPromotions } from "./pricing-fixture";
const api = await import("@/lib/sales-api");
const { payCart } = await import("@/app/(app)/cart-actions");
const { CheckoutZone } = await import("@/components/checkout-zone");
const { default: SalesPage } = await import("@/app/(app)/page");

type Payment = Awaited<ReturnType<typeof payCart>>;

// AC-talad-027 · 028: ส้มสายน้ำผึ้ง ×2 bound to สมหญิง ใจดี (5%) — 85.50 to pay
const sale = { id: 31, receiptNo: "20260924-000007", paidAt: "2026-09-24T10:00:00+07:00", sellerName: "สมชาย", lines: [], netTotal: 85.5, status: "Paid" };
// AC-talad-127: two promotions that give ส้มสายน้ำผึ้ง ×3 the same 13.50
const tie: PromotionChoice[] = [
  { id: 41, name: "ส้มสายน้ำผึ้ง ลด 10%", discount: 13.5 },
  { id: 42, name: "ซื้อ ส้มสายน้ำผึ้ง 3 ชิ้น ลด 10%", discount: 13.5 },
];
const paid = (netTotal = 85.5, receiptNo = sale.receiptNo): Payment => ({ ok: true, sale: { id: sale.id, receiptNo, netTotal } });
const reply = (status: number, body: unknown) => new Response(JSON.stringify(body), { status });
const checkoutButton = () => screen.getByTestId("ui-talad-002-checkout") as HTMLButtonElement;
/** Press ชำระเงิน and wait until the payment has settled (the transition's state "loading" is over). */
async function press() {
  await userEvent.click(checkoutButton());
  await waitFor(() => expect(checkoutButton().disabled).toBe(false));
}
/** Press a button in UI-talad-003 and wait the same way. */
async function choose(button: HTMLElement) {
  await userEvent.click(button);
  await waitFor(() => expect(checkoutButton().disabled).toBe(false));
}

/** A payment the test lets go of when it chooses — the button's state "loading" is what happens meanwhile. */
function held() {
  let release!: (p: Payment) => void;
  const promise = new Promise<Payment>((resolve) => (release = resolve));
  return { promise, release };
}

beforeEach(() => {
  // jsdom draws <dialog> but does not implement showModal/close
  HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) { this.open = true; };
  HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) { this.open = false; };
  cache.revalidatePath.mockClear();
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FE-talad-034 · API-010 on the web", () => {
  it("pays the cart the screen showed, with the seller's token and each choice in order", async () => {
    const fetch = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(async () => reply(201, sale));
    vi.stubGlobal("fetch", fetch);

    const outcome = await api.checkout(11, [41, 57]);

    const [url, init] = fetch.mock.calls[0];
    expect(new URL(url).pathname).toBe("/api/cart/checkout");
    expect(init?.method).toBe("POST");
    expect(JSON.parse(String(init?.body))).toEqual({ cartId: 11, choice: [41, 57] });
    expect(new Headers(init?.headers).get("authorization")).toBe("Bearer jwt-somchai");
    // only what the till hands on crosses to the client (server-serialization)
    expect(outcome).toEqual({ ok: true, sale: { id: 31, receiptNo: "20260924-000007", netTotal: 85.5 } });
  });

  it("tells a tie, a refusal and a lost session apart by code — the refusal's sentence as the api worded it (AC-talad-070)", async () => {
    const answers = [
      reply(409, { code: "PROMOTION_CHOICE_NEEDED", message: "มีโปรโมชั่นที่ลดเท่ากัน กรุณาเลือกโปรโมชั่น", choices: tie }),
      reply(409, { code: "INSUFFICIENT_STOCK", message: "ส้มสายน้ำผึ้ง คงเหลือไม่พอ (เหลือ 1)" }),
      reply(409, { code: "PRODUCT_DISCONTINUED", message: "ส้มสายน้ำผึ้ง เลิกขายแล้ว กรุณาเอาออกจากตะกร้า" }),
      reply(409, { code: "CART_NOT_OPEN", message: "ตะกร้านี้ชำระเงินไปแล้ว" }),
      new Response(null, { status: 401 }),
      reply(500, {}),
    ];
    vi.stubGlobal("fetch", vi.fn(async () => answers.shift()!));

    expect(await api.checkout(11, [])).toEqual({ ok: false, code: "PROMOTION_CHOICE_NEEDED", choices: tie });
    expect(await api.checkout(11, [])).toEqual({ ok: false, code: "REFUSED", message: "ส้มสายน้ำผึ้ง คงเหลือไม่พอ (เหลือ 1)" });
    expect(await api.checkout(11, [])).toEqual({ ok: false, code: "REFUSED", message: "ส้มสายน้ำผึ้ง เลิกขายแล้ว กรุณาเอาออกจากตะกร้า" });
    expect(await api.checkout(11, [])).toEqual({ ok: false, code: "REFUSED", message: "ตะกร้านี้ชำระเงินไปแล้ว" });
    expect(await api.checkout(11, [])).toEqual({ ok: false, code: "SIGNED_OUT" });
    await expect(api.checkout(11, [])).rejects.toThrow("answered 500");
  });

  it("the action reads the page again once paid, so the api's new empty cart replaces the paid one (AC-talad-004)", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => reply(201, sale)));

    expect(await payCart(11, [])).toEqual(paid());
    expect(cache.revalidatePath).toHaveBeenCalledWith("/");
  });

  it("the action hands a tie back for UI-talad-003 and reads nothing again — nothing was paid", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => reply(409, { code: "PROMOTION_CHOICE_NEEDED", message: "…", choices: tie })));

    expect(await payCart(11, [])).toEqual({ ok: false, choices: tie });
    expect(cache.revalidatePath).not.toHaveBeenCalled();
  });

  it("the action says the line dropped when the api cannot be reached, and sends a lost session to sign in again", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("fetch failed"); }));
    expect(await payCart(11, [])).toEqual({ ok: false, message: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่" });

    vi.stubGlobal("fetch", vi.fn(async () => new Response(null, { status: 401 })));
    await expect(payCart(11, [])).rejects.toThrow("NEXT_REDIRECT /logout");
  });
});

describe("FE-talad-034 · UI-talad-002 ชำระเงิน", () => {
  it("state empty — the button cannot be pressed while the cart has nothing in it", () => {
    render(<CheckoutZone cartId={11} empty pay={vi.fn()} />);
    expect((checkoutButton() as HTMLButtonElement).disabled).toBe(true);
  });

  it("the sales page draws ชำระเงิน in the till for the cart it read, off while that cart is empty", async () => {
    const cart = { id: 11, status: "Open", lines: [], subtotal: 0 };
    vi.stubGlobal(
      "fetch",
      vi.fn(async (url: string) => {
        const path = new URL(url).pathname;
        const body = path === "/api/cart/pricing" ? noPromotions(cart) : path === "/api/cart" ? cart : { items: [], page: 1, pageSize: 20, total: 0 };
        return reply(200, body);
      }),
    );

    render(await SalesPage({ searchParams: Promise.resolve({}) }));

    expect((within(document.querySelector(".till") as HTMLElement).getByTestId("ui-talad-002-checkout") as HTMLButtonElement).disabled).toBe(true);
  });

  it("pays the cart shown and says which bill was made; a 0-baht bill is paid like any other (AC-talad-092)", async () => {
    const pay = vi.fn(async () => paid(0, "20260924-000008"));
    render(<CheckoutZone cartId={11} empty={false} pay={pay} />);

    await press();

    expect(pay).toHaveBeenCalledWith(11, []);
    expect(screen.getByRole("status").textContent).toContain("ชำระเงินสำเร็จ · ใบเสร็จ 20260924-000008 · ฿0.00");
  });

  it("AC-talad-027 — two presses one after the other send one payment; the button is off while it is on its way", async () => {
    const payment = held();
    const pay = vi.fn(() => payment.promise);
    render(<CheckoutZone cartId={11} empty={false} pay={pay} />);

    const button = checkoutButton();
    await act(async () => {
      button.click();
      button.click(); // the same frame — before the transition has drawn the button off
    });
    await userEvent.click(button);

    expect(pay).toHaveBeenCalledTimes(1);
    expect((button as HTMLButtonElement).disabled).toBe(true);
    expect(button.textContent).toContain("กำลังชำระเงิน…");

    await act(async () => payment.release(paid()));
    expect((button as HTMLButtonElement).disabled).toBe(false);
    expect(screen.getByRole("status").textContent).toContain("20260924-000007");
  });

  it("AC-talad-028 — after the line dropped, pressing again sends the same cart and shows that it was already paid", async () => {
    // the api saved it, and the browser never got the answer: the server action itself rejects on the client
    const pay = vi
      .fn<(cartId: number, choice: number[]) => Promise<Payment>>()
      .mockRejectedValueOnce(new TypeError("Failed to fetch"))
      .mockResolvedValueOnce({ ok: false, message: "ตะกร้านี้ชำระเงินไปแล้ว" });
    render(<CheckoutZone cartId={11} empty={false} pay={pay} />);

    await press();
    expect(screen.getByRole("alert").textContent).toContain("เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่");

    await press();
    expect(pay.mock.calls).toEqual([[11, []], [11, []]]);
    expect(screen.getByRole("alert").textContent).toContain("ตะกร้านี้ชำระเงินไปแล้ว");
    expect(screen.queryByRole("status")).toBeNull();
  });

  it("state error — a refusal shows the rule's sentence and the button can be pressed again (AC-talad-070 · 087)", async () => {
    const answers: Payment[] = [
      { ok: false, message: "ส้มสายน้ำผึ้ง คงเหลือไม่พอ (เหลือ 1)" },
      { ok: false, message: "ส้มสายน้ำผึ้ง เลิกขายแล้ว กรุณาเอาออกจากตะกร้า" },
    ];
    render(<CheckoutZone cartId={11} empty={false} pay={vi.fn(async () => answers.shift()!)} />);

    await press();
    expect(screen.getByRole("alert").textContent).toContain("ส้มสายน้ำผึ้ง คงเหลือไม่พอ (เหลือ 1)");
    expect((checkoutButton() as HTMLButtonElement).disabled).toBe(false);

    await press();
    expect(screen.getByRole("alert").textContent).toContain("ส้มสายน้ำผึ้ง เลิกขายแล้ว กรุณาเอาออกจากตะกร้า");
  });

  it("AC-talad-127 — a tie opens UI-talad-003 with each promotion and its baht; เลือกโปรนี้ pays with that choice", async () => {
    const answers: Payment[] = [{ ok: false, choices: tie }, paid(121.5)];
    const pay = vi.fn(async () => answers.shift()!);
    render(<CheckoutZone cartId={11} empty={false} pay={pay} />);

    await press();

    const window = screen.getByRole("dialog", { hidden: true });
    expect(window).toHaveProperty("open", true);
    const rows = within(window).getAllByRole("row", { hidden: true }).slice(1);
    expect(rows.map((r) => r.textContent)).toEqual([
      expect.stringContaining("ส้มสายน้ำผึ้ง ลด 10%−฿13.50"),
      expect.stringContaining("ซื้อ ส้มสายน้ำผึ้ง 3 ชิ้น ลด 10%−฿13.50"),
    ]);

    await choose(within(rows[0]).getByTestId("ui-talad-003-choose-promo"));

    expect(pay.mock.calls).toEqual([[11, []], [11, [41]]]);
    expect(window).toHaveProperty("open", false);
    expect(screen.getByRole("status").textContent).toContain("฿121.50");
  });

  it("a second tied round asks again, and the payment carries both choices in order", async () => {
    const other: PromotionChoice[] = [
      { id: 51, name: "ลดทั้งบิล 5%", discount: 10 },
      { id: 52, name: "ลดทั้งบิล 10 บาท", discount: 10 },
    ];
    const answers: Payment[] = [{ ok: false, choices: tie }, { ok: false, choices: other }, paid()];
    const pay = vi.fn(async () => answers.shift()!);
    render(<CheckoutZone cartId={11} empty={false} pay={pay} />);

    await press();
    await choose(screen.getAllByTestId("ui-talad-003-choose-promo")[1]);
    expect(screen.getAllByTestId("ui-talad-003-ent-006-name").map((n) => n.textContent)).toEqual(["ลดทั้งบิล 5%", "ลดทั้งบิล 10 บาท"]);
    await choose(screen.getAllByTestId("ui-talad-003-choose-promo")[0]);

    expect(pay.mock.calls).toEqual([[11, []], [11, [42]], [11, [42, 51]]]);
  });

  it("ยกเลิก closes the window with nothing paid, and the next press starts without the earlier choice", async () => {
    const answers: Payment[] = [{ ok: false, choices: tie }, { ok: false, choices: tie }];
    const pay = vi.fn(async () => answers.shift()!);
    render(<CheckoutZone cartId={11} empty={false} pay={pay} />);

    await press();
    await choose(screen.getByTestId("ui-talad-003-cancel"));
    expect(screen.getByRole("dialog", { hidden: true })).toHaveProperty("open", false);
    expect(screen.queryByRole("status")).toBeNull();

    await press();
    expect(pay.mock.calls).toEqual([[11, []], [11, []]]);
  });
});
