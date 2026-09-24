import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { ProductDetail, StockAdjustment } from "@/lib/products-api";

const revalidatePath = vi.hoisted(() => vi.fn());
const redirect = vi.hoisted(() =>
  vi.fn((to: string) => {
    throw new Error(`NEXT_REDIRECT ${to}`);
  }),
);
vi.mock("next/cache", () => ({ revalidatePath }));
vi.mock("next/headers", () => ({ cookies: async () => ({ get: () => ({ value: "jwt-owner" }) }) }));
vi.mock("next/navigation", () => ({ redirect: (to: string) => redirect(to) }));
vi.mock("@/lib/me", () => ({ requireScreen: async () => ({ username: "owner" }) }));

const { adjustStockAction } = await import("@/app/(app)/stock/[id]/actions");
const { default: ProductPage } = await import("@/app/(app)/stock/[id]/page");
const { AdjustStockDialog } = await import("@/components/adjust-stock-dialog");
const { StockAdjustments } = await import("@/components/stock-adjustments");
const { PriceHistory } = await import("@/components/price-history");

const NEGATIVE = "จำนวนคงเหลือต้องไม่ติดลบ (เหลือ 3)";
const DUPLICATE = "รายการปรับสต็อกนี้บันทึกไปแล้ว";

// AC-talad-075 · 078: +10 รับของเข้า at 09:10 on 23 ก.ย. 2569 (Thai time), then −2 ของเน่า/เสีย, newest first
const received: StockAdjustment = {
  id: 1,
  reason: "RECEIVE",
  quantityDelta: 10,
  countedQty: null,
  note: "ของจากสวน",
  adjustedById: 1,
  adjustedByName: "เจ้าของร้าน",
  adjustedAt: "2026-09-23T02:10:00Z",
};
const spoiled: StockAdjustment = { ...received, id: 2, reason: "SPOILED", quantityDelta: -2, note: null, adjustedAt: "2026-09-23T03:00:00Z" };
const pageOf = <T,>(items: T[], total = items.length, page = 1) => ({ items, page, pageSize: 20, total });
const orange: ProductDetail = {
  id: 1,
  name: "ส้มสายน้ำผึ้ง",
  barcode: "8850000000011",
  stockQty: 4,
  lowStockThreshold: 5,
  lowStock: true,
  price: 45,
  hasImage: false,
  priceHistory: pageOf([]),
  adjustments: pageOf([]),
};

function apiAnswers(status: number, body?: unknown) {
  return vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(
    async () => new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { "content-type": "application/json" } }),
  );
}

const refused = (field: string, message: string) => apiAnswers(400, { code: "STOCK_ADJUSTMENT_INVALID", errors: [{ field, message }] });

function form(fields: Record<string, string>) {
  const fd = new FormData();
  for (const [k, v] of Object.entries({ requestKey: "form-1", reason: "", quantity: "", countedQty: "", note: "", ...fields })) fd.set(k, v);
  return fd;
}

const sentBody = (fetch: ReturnType<typeof apiAnswers>) => JSON.parse(String(fetch.mock.calls[0][1]?.body));

beforeEach(() => {
  // jsdom draws <dialog> but does not implement showModal/close
  HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) { this.open = true; };
  HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) { this.open = false; };
  revalidatePath.mockClear();
  redirect.mockClear();
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FE-talad-024 · UI-talad-014 save action (API-024)", () => {
  it("AC-talad-075 · รับของเข้า +10 goes with the note and the form's key, with the owner's token; the detail is redrawn", async () => {
    const fetch = apiAnswers(201, { ...orange, stockQty: 14, adjustments: pageOf([received]) });
    vi.stubGlobal("fetch", fetch);

    const answer = await adjustStockAction(1, undefined, form({ reason: "RECEIVE", quantity: "10", note: "ของจากสวน" }));

    expect(answer?.saved).toBe(true);
    const [url, init] = fetch.mock.calls[0];
    expect([new URL(url).pathname, init?.method]).toEqual(["/api/products/1/stock-adjustments", "POST"]);
    expect(sentBody(fetch)).toEqual({ reason: "RECEIVE", quantity: 10, countedQty: null, note: "ของจากสวน", requestKey: "form-1" });
    expect(new Headers(init?.headers).get("authorization")).toBe("Bearer jwt-owner");
    expect(revalidatePath).toHaveBeenCalledWith("/stock/1");
  });

  it("AC-talad-076 · นับสต็อกใหม่ sends what was counted, not a change", async () => {
    const fetch = apiAnswers(201, orange);
    vi.stubGlobal("fetch", fetch);

    await adjustStockAction(1, undefined, form({ reason: "RECOUNT", quantity: "5", countedQty: "8" }));

    expect(sentBody(fetch)).toMatchObject({ reason: "RECOUNT", quantity: null, countedQty: 8, note: null });
  });

  it("a − typed as U+2212 is read as a minus", async () => {
    const fetch = apiAnswers(201, orange);
    vi.stubGlobal("fetch", fetch);

    await adjustStockAction(1, undefined, form({ reason: "SPOILED", quantity: "−2" }));

    expect(sentBody(fetch)).toMatchObject({ reason: "SPOILED", quantity: -2 });
  });

  it("AC-talad-077 · state error · the rule's sentence sits under จำนวน, what was typed stays, nothing is redrawn", async () => {
    vi.stubGlobal("fetch", refused("quantity", NEGATIVE));

    const answer = await adjustStockAction(1, undefined, form({ reason: "SPOILED", quantity: "-5" }));

    expect(answer).toEqual({ values: { reason: "SPOILED", quantity: "-5", countedQty: "", note: "" }, errors: { quantity: NEGATIVE } });
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it.each(["AC-talad-079", "AC-talad-080"])("%s · the same form saved already: the rule's sentence, and the detail shows the stock that was saved", async () => {
    vi.stubGlobal("fetch", apiAnswers(409, { code: "STOCK_ADJUSTMENT_DUPLICATE", errors: [{ field: "requestKey", message: DUPLICATE }] }));

    const answer = await adjustStockAction(1, undefined, form({ reason: "RECEIVE", quantity: "10" }));

    expect(answer?.message).toBe(DUPLICATE);
    expect(answer?.saved).toBeUndefined();
    expect(revalidatePath).toHaveBeenCalledWith("/stock/1");
  });

  it("a form with no key is told to open a new one", async () => {
    vi.stubGlobal("fetch", refused("requestKey", "ฟอร์มนี้ไม่มีคีย์กันบันทึกซ้ำ กรุณาเปิดฟอร์มใหม่"));

    expect((await adjustStockAction(1, undefined, form({ requestKey: "" })))?.message).toBe("ฟอร์มนี้ไม่มีคีย์กันบันทึกซ้ำ กรุณาเปิดฟอร์มใหม่");
  });

  it("a product discontinued since the page opened — the stock list says ไม่พบสินค้า", async () => {
    vi.stubGlobal("fetch", apiAnswers(404, { code: "PRODUCT_NOT_FOUND", errors: [] }));

    await expect(adjustStockAction(1, undefined, form({ reason: "RECEIVE", quantity: "10" }))).rejects.toThrow("NEXT_REDIRECT /stock?gone=1");
  });

  it("ACL-022 · 403 keeps the form with the sentence of BR-talad-018 · 401 signs out · no server keeps what was typed", async () => {
    vi.stubGlobal("fetch", apiAnswers(403));
    expect((await adjustStockAction(1, undefined, form({ reason: "RECEIVE", quantity: "10" })))?.message).toBe("คุณไม่มีสิทธิ์เข้าถึงหน้านี้");

    vi.stubGlobal("fetch", apiAnswers(401));
    await expect(adjustStockAction(1, undefined, form({ reason: "RECEIVE", quantity: "10" }))).rejects.toThrow("NEXT_REDIRECT /logout");

    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("fetch failed"); }));
    const offline = await adjustStockAction(1, undefined, form({ reason: "RECEIVE", quantity: "10" }));
    expect([offline?.message, offline?.values.quantity]).toEqual(["เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่", "10"]);
  });
});

describe("FE-talad-024 · UI-talad-014 window", () => {
  const keyOf = () => (document.querySelector("dialog input[name=requestKey]") as HTMLInputElement).value;

  it("ปรับสต็อก opens the window with the stock now and the three reasons; ยกเลิก closes it without saving", async () => {
    const action = vi.fn(async () => undefined);
    render(<AdjustStockDialog current={4} action={action} />);

    await userEvent.click(screen.getByTestId("ui-talad-012-adjust-stock"));
    const dialog = document.querySelector("dialog")!;
    expect(dialog.open).toBe(true);
    expect(within(dialog).getByTestId("ui-talad-014-ent-001-stock-qty").textContent).toBe("4");
    const reasons = [...(within(dialog).getByTestId("ui-talad-014-ent-003-reason") as HTMLSelectElement).options].map((o) => [o.value, o.text]);
    expect(reasons.slice(1)).toEqual([["RECEIVE", "รับของเข้า"], ["SPOILED", "ของเน่า/เสีย"], ["RECOUNT", "นับสต็อกใหม่"]]);

    await userEvent.click(within(dialog).getByTestId("ui-talad-014-cancel"));
    expect(dialog.open).toBe(false);
    expect(action).not.toHaveBeenCalled();
  });

  it("AC-talad-079 · 080 · sending this form again sends the same key", async () => {
    const keys: string[] = [];
    const action = vi.fn(async (_prev: unknown, fd: FormData) => {
      keys.push(String(fd.get("requestKey")));
      return { values: { reason: "RECEIVE", quantity: "10", countedQty: "", note: "" }, errors: {}, message: DUPLICATE };
    });
    render(<AdjustStockDialog current={4} action={action} />);

    await userEvent.click(screen.getByTestId("ui-talad-012-adjust-stock"));
    await userEvent.selectOptions(screen.getByTestId("ui-talad-014-ent-003-reason"), "RECEIVE");
    await userEvent.type(screen.getByTestId("ui-talad-014-ent-003-quantity-delta"), "10");
    await userEvent.click(screen.getByTestId("ui-talad-014-save"));
    await screen.findByText(DUPLICATE);
    await userEvent.click(screen.getByTestId("ui-talad-014-save"));

    await waitFor(() => expect(action).toHaveBeenCalledTimes(2));
    expect(keys[0]).toMatch(/^[0-9a-f]{32}$/);
    expect(keys[1]).toBe(keys[0]);
  });

  it("AC-talad-081 · a window opened again is a new form with a new key and nothing carried over", async () => {
    render(<AdjustStockDialog current={4} action={async () => ({ values: { reason: "RECEIVE", quantity: "10", countedQty: "", note: "" }, errors: {}, saved: true })} />);

    await userEvent.click(screen.getByTestId("ui-talad-012-adjust-stock"));
    const first = keyOf();
    await userEvent.selectOptions(screen.getByTestId("ui-talad-014-ent-003-reason"), "RECEIVE");
    await userEvent.type(screen.getByTestId("ui-talad-014-ent-003-quantity-delta"), "10");
    await userEvent.click(screen.getByTestId("ui-talad-014-save"));
    await waitFor(() => expect(document.querySelector("dialog")!.open).toBe(false));

    await userEvent.click(screen.getByTestId("ui-talad-012-adjust-stock"));

    expect(keyOf()).not.toBe(first);
    expect((screen.getByTestId("ui-talad-014-ent-003-quantity-delta") as HTMLInputElement).value).toBe("");
  });

  it("AC-talad-077 · state error · the sentence under its field, the form as typed — twice in a row", async () => {
    const action = vi.fn(async (_prev: unknown, fd: FormData) => ({
      values: { reason: String(fd.get("reason")), quantity: String(fd.get("quantity")), countedQty: "", note: String(fd.get("note")) },
      errors: { quantity: NEGATIVE },
    }));
    render(<AdjustStockDialog current={3} action={action} />);

    await userEvent.click(screen.getByTestId("ui-talad-012-adjust-stock"));
    await userEvent.selectOptions(screen.getByTestId("ui-talad-014-ent-003-reason"), "SPOILED");
    await userEvent.type(screen.getByTestId("ui-talad-014-ent-003-quantity-delta"), "-5");
    await userEvent.type(screen.getByTestId("ui-talad-014-ent-003-note"), "ส้มช้ำ");
    await userEvent.click(screen.getByTestId("ui-talad-014-save"));
    await screen.findByText(NEGATIVE);
    await userEvent.click(screen.getByTestId("ui-talad-014-save"));
    await waitFor(() => expect(action).toHaveBeenCalledTimes(2));

    const quantity = screen.getByTestId("ui-talad-014-ent-003-quantity-delta") as HTMLInputElement;
    expect(screen.getByText(NEGATIVE).closest("label")?.contains(quantity)).toBe(true);
    expect([(screen.getByTestId("ui-talad-014-ent-003-reason") as HTMLSelectElement).value, quantity.value]).toEqual(["SPOILED", "-5"]);
    expect((screen.getByTestId("ui-talad-014-ent-003-note") as HTMLTextAreaElement).value).toBe("ส้มช้ำ");
    expect(document.querySelector("dialog")!.open).toBe(true);
    expect(screen.getByTestId("ui-talad-014-save").closest("form")?.noValidate).toBe(true);
  });

  it("state loading · save is off while saving, so it cannot be pressed twice", async () => {
    let finish: () => void = () => {};
    const action = vi.fn(() => new Promise<undefined>((resolve) => (finish = () => resolve(undefined))));
    render(<AdjustStockDialog current={4} action={action} />);

    await userEvent.click(screen.getByTestId("ui-talad-012-adjust-stock"));
    await userEvent.click(screen.getByTestId("ui-talad-014-save"));

    expect((screen.getByTestId("ui-talad-014-save") as HTMLButtonElement).disabled).toBe(true);
    finish();
  });
});

describe("FE-talad-024 · UI-talad-012 adjustments section", () => {
  it("AC-talad-075 · 078 · both rows newest first, keyed by their id, with the change, the reason, who, when (Thai time) and the note", () => {
    render(<StockAdjustments adjustments={pageOf([spoiled, received])} productId={1} />);

    const deltas = screen.getAllByTestId("ui-talad-012-ent-003-quantity-delta");
    expect(deltas.map((c) => [c.closest("tr")?.getAttribute("data-row-key"), c.textContent])).toEqual([
      ["2", "−2"],
      ["1", "+10"],
    ]);
    expect(screen.getAllByTestId("ui-talad-012-ent-003-reason").map((c) => c.textContent)).toEqual(["ของเน่า/เสีย", "รับของเข้า"]);
    expect(screen.getAllByTestId("ui-talad-012-ent-003-adjusted-by").map((c) => c.textContent)).toEqual(["เจ้าของร้าน", "เจ้าของร้าน"]);
    const at = screen.getAllByTestId("ui-talad-012-ent-003-adjusted-at")[1].textContent;
    expect(at).toContain("09:10");
    expect(at).toContain("2569");
    expect(screen.getAllByTestId("ui-talad-012-ent-003-note").map((c) => c.textContent)).toEqual(["", "ของจากสวน"]);
  });

  it("state empty · no adjustment yet: ยังไม่มีรายการ", () => {
    render(<StockAdjustments adjustments={pageOf([])} productId={1} />);

    expect(screen.getByText("ยังไม่มีรายการ")).toBeTruthy();
  });

  it("state overflow · 21 rows page at 20 on adjustmentsPage, keeping the price history where it was — and the other way round", () => {
    const twenty = Array.from({ length: 20 }, (_, i) => ({ ...received, id: 100 + i }));
    render(<StockAdjustments adjustments={pageOf(twenty, 21)} productId={1} historyPage={3} />);

    expect(screen.getAllByTestId("ui-talad-012-ent-003-quantity-delta")).toHaveLength(20);
    expect(screen.getByText("ถัดไป ›").getAttribute("href")).toBe("/stock/1?historyPage=3&adjustmentsPage=2");
    cleanup();

    const change = { id: 6, previousPrice: 45, price: 50, source: "STOCK_SCREEN", changedById: 1, changedByName: "เจ้าของร้าน", changedAt: "2026-09-23T03:00:00Z" };
    render(<PriceHistory history={pageOf(Array.from({ length: 20 }, (_, i) => ({ ...change, id: 200 + i })), 21)} productId={1} adjustmentsPage={2} />);
    expect(screen.getByText("ถัดไป ›").getAttribute("href")).toBe("/stock/1?historyPage=2&adjustmentsPage=2");
  });

  it("the product page asks the api for the adjustments page and draws ปรับสต็อก and the section", async () => {
    const fetch = apiAnswers(200, { ...orange, adjustments: pageOf([received]) });
    vi.stubGlobal("fetch", fetch);

    render(await ProductPage({ params: Promise.resolve({ id: "1" }), searchParams: Promise.resolve({ adjustmentsPage: "2" }) }));

    const url = new URL(fetch.mock.calls[0][0]);
    expect([url.searchParams.get("historyPage"), url.searchParams.get("adjustmentsPage")]).toEqual(["1", "2"]);
    expect(screen.getByTestId("ui-talad-012-adjust-stock").textContent).toBe("ปรับสต็อก");
    expect(screen.getByTestId("ui-talad-012-ent-003-quantity-delta").textContent).toBe("+10");
  });
});
