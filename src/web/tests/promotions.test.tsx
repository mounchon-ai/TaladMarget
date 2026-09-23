import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { Promotion } from "@/lib/promotions-api";

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

const { savePromotionAction, findProducts } = await import("@/app/(app)/promotions/actions");
const { default: PromotionsPage } = await import("@/app/(app)/promotions/page");
const { default: NewPromotionPage } = await import("@/app/(app)/promotions/new/page");
const { default: EditPromotionPage } = await import("@/app/(app)/promotions/[id]/page");
const { PromotionList, HeaderAddPromotion } = await import("@/components/promotion-list");
const { PromotionForm } = await import("@/components/promotion-form");
const { EMPTY_VALUES, valuesOf } = await import("@/lib/promotion-form");

const RATE_OUT_OF_RANGE = "ส่วนลดต้องเป็นจำนวนเต็ม 0–100";
const END_BEFORE_START = "วันสิ้นสุดต้องไม่ก่อนวันเริ่ม";
const REQUIRED = "กรุณากรอกช่องนี้";

// AC-talad-062's promotion: ส้มสายน้ำผึ้ง 10% off
const orange = { id: 31, name: "ส้มสายน้ำผึ้ง" };
const tenPercent: Promotion = {
  id: 5,
  status: "Active",
  versionId: 40,
  name: "ส้มลด 10%",
  type: "ITEM_PERCENT",
  productA: orange,
  qtyA: null,
  productB: null,
  qtyB: null,
  freeProduct: null,
  freeQty: null,
  ratePercent: 10,
  minSubtotal: null,
  startDate: "2026-09-01",
  endDate: null,
  changedAt: "2026-09-01T02:00:00Z",
};
const twentyPercent = { ...tenPercent, versionId: 41, name: "ส้มลด 20%", ratePercent: 20, changedAt: "2026-09-23T09:00:00Z" };
const billPercent = { ...tenPercent, id: 6, versionId: 42, name: "ลดทั้งบิล 5%", type: "BILL_PERCENT", productA: null, ratePercent: 5, minSubtotal: 500, endDate: "2026-12-31" };
const pageOf = (items: Promotion[], total = items.length, page = 1) => ({ items, page, pageSize: 20, total });

function apiAnswers(status: number, body: unknown) {
  return vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(
    async () => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } }),
  );
}

function form(entries: Record<string, string>) {
  const fd = new FormData();
  for (const [k, v] of Object.entries(entries)) fd.set(k, v);
  return fd;
}

const orangeTenPercent = { name: "ส้มลด 10%", type: "ITEM_PERCENT", productA: "31", productAName: "ส้มสายน้ำผึ้ง", ratePercent: "10", startDate: "2026-09-01", endDate: "" };

beforeEach(() => {
  revalidatePath.mockClear();
  redirect.mockClear();
  requireScreen.mockClear();
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FE-talad-026 · UI-talad-016 save action (API-027 · API-028)", () => {
  it("UC-talad-021 · a new promotion is POSTed to /api/promotions with the owner's token, numbers as numbers, then the list says so", async () => {
    const fetch = apiAnswers(201, tenPercent);
    vi.stubGlobal("fetch", fetch);

    await expect(savePromotionAction(null, undefined, form(orangeTenPercent))).rejects.toThrow("NEXT_REDIRECT /promotions?saved=1");

    const [url, init] = fetch.mock.calls[0];
    expect([new URL(url).pathname, init?.method]).toEqual(["/api/promotions", "POST"]);
    expect(new Headers(init?.headers).get("authorization")).toBe("Bearer jwt-owner");
    expect(JSON.parse(String(init?.body))).toEqual({
      name: "ส้มลด 10%",
      type: "ITEM_PERCENT",
      productA: 31,
      qtyA: null,
      productB: null,
      qtyB: null,
      freeProduct: null,
      freeQty: null,
      ratePercent: 10,
      minSubtotal: null,
      startDate: "2026-09-01",
      endDate: null,
    });
    expect(revalidatePath).toHaveBeenCalledWith("/promotions");
  });

  it("AC-talad-062 · editing 10% to 20% adds a version of promotion 5 (POST …/5/versions), never a second promotion", async () => {
    const fetch = apiAnswers(201, twentyPercent);
    vi.stubGlobal("fetch", fetch);

    await expect(savePromotionAction(5, undefined, form({ ...orangeTenPercent, name: "ส้มลด 20%", ratePercent: "20" }))).rejects.toThrow(
      "NEXT_REDIRECT /promotions?saved=1",
    );

    expect(fetch).toHaveBeenCalledTimes(1);
    const [url, init] = fetch.mock.calls[0];
    expect([new URL(url).pathname, init?.method]).toEqual(["/api/promotions/5/versions", "POST"]);
    expect(JSON.parse(String(init?.body))).toMatchObject({ productA: 31, ratePercent: 20 });
  });

  it("state error · each message under its field, the typed values stay and nothing is revalidated", async () => {
    vi.stubGlobal(
      "fetch",
      apiAnswers(400, {
        code: "PROMOTION_INVALID",
        errors: [
          { field: "ratePercent", message: RATE_OUT_OF_RANGE },
          { field: "endDate", message: END_BEFORE_START },
        ],
      }),
    );
    const typed = { ...orangeTenPercent, ratePercent: "150", endDate: "2026-08-01" };

    const state = await savePromotionAction(null, undefined, form(typed));

    expect(state?.errors).toEqual({ ratePercent: RATE_OUT_OF_RANGE, endDate: END_BEFORE_START });
    expect(state?.values).toMatchObject(typed);
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it("an empty box or one that is not a number goes as null, for the domain to answer under that field", async () => {
    const fetch = apiAnswers(400, { code: "PROMOTION_INVALID", errors: [{ field: "qtyA", message: REQUIRED }] });
    vi.stubGlobal("fetch", fetch);

    await savePromotionAction(null, undefined, form({ ...orangeTenPercent, type: "BUY_X_PERCENT", qtyA: "abc", ratePercent: "" }));

    expect(JSON.parse(String(fetch.mock.calls[0][1]?.body))).toMatchObject({ qtyA: null, ratePercent: null });
  });

  it("a promotion discontinued while its page was open goes back to the list saying ไม่พบโปรโมชั่น", async () => {
    vi.stubGlobal("fetch", apiAnswers(404, { code: "PROMOTION_NOT_FOUND", errors: [] }));

    await expect(savePromotionAction(5, undefined, form(orangeTenPercent))).rejects.toThrow("NEXT_REDIRECT /promotions?gone=1");
  });

  it("a seller refused by the api gets the no-permission sentence; an expired session signs out; no server asks to try again", async () => {
    vi.stubGlobal("fetch", apiAnswers(403, {}));
    expect((await savePromotionAction(null, undefined, form(orangeTenPercent)))?.message).toBe("คุณไม่มีสิทธิ์เข้าถึงหน้านี้");

    vi.stubGlobal("fetch", apiAnswers(401, {}));
    await expect(savePromotionAction(null, undefined, form(orangeTenPercent))).rejects.toThrow("NEXT_REDIRECT /logout");

    vi.stubGlobal("fetch", vi.fn(async () => Promise.reject(new Error("ECONNREFUSED"))));
    expect((await savePromotionAction(null, undefined, form(orangeTenPercent)))?.message).toBe("เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่");
  });

  it("state overflow · the product fields search at the server (API-003), 20 at most, never the whole catalog", async () => {
    const fetch = apiAnswers(200, { items: [{ ...orange, barcode: null, price: 45, stockQty: 9, lowStock: false, hasImage: false }], page: 1, pageSize: 20, total: 1 });
    vi.stubGlobal("fetch", fetch);

    expect(await findProducts(" ส้ม ")).toEqual([orange]);
    const url = new URL(fetch.mock.calls[0][0]);
    expect([url.pathname, url.searchParams.get("search"), url.searchParams.get("page")]).toEqual(["/api/products", "ส้ม", "1"]);

    vi.stubGlobal("fetch", apiAnswers(500, {}));
    expect(await findProducts("ส้ม")).toBeNull();
  });
});

describe("FE-talad-026 · UI-talad-016 form", () => {
  const noProducts = async () => [];

  it("only the conditions the chosen form uses are drawn", async () => {
    render(<PromotionForm initial={EMPTY_VALUES} action={async () => undefined} find={noProducts} />);
    const drawn = () =>
      ["product-a", "qty-a", "product-b", "qty-b", "free-product", "free-qty", "rate-percent", "min-subtotal"].filter((f) =>
        screen.queryByTestId(`ui-talad-016-ent-006-${f}`),
      );

    expect(drawn()).toEqual([]);
    await userEvent.selectOptions(screen.getByTestId("ui-talad-016-ent-006-type"), "BUY_AB_GET_Y");
    expect(drawn()).toEqual(["product-a", "qty-a", "product-b", "qty-b", "free-product", "free-qty"]);
    await userEvent.selectOptions(screen.getByTestId("ui-talad-016-ent-006-type"), "BILL_PERCENT");
    expect(drawn()).toEqual(["rate-percent", "min-subtotal"]);
    await userEvent.selectOptions(screen.getByTestId("ui-talad-016-ent-006-type"), "BUY_X_PERCENT");
    expect(drawn()).toEqual(["product-a", "qty-a", "rate-percent"]);
  });

  it("AC-talad-062 · editing opens with the conditions in force — ส้มสายน้ำผึ้ง, 10%", () => {
    render(<PromotionForm initial={valuesOf(tenPercent)} action={async () => undefined} find={noProducts} />);

    expect((screen.getByTestId("ui-talad-016-ent-006-name") as HTMLInputElement).value).toBe("ส้มลด 10%");
    expect((screen.getByTestId("ui-talad-016-ent-006-type") as HTMLSelectElement).value).toBe("ITEM_PERCENT");
    expect((screen.getByTestId("ui-talad-016-ent-006-product-a") as HTMLInputElement).value).toBe("ส้มสายน้ำผึ้ง");
    expect((document.querySelector('input[name="productA"]') as HTMLInputElement).value).toBe("31");
    expect((screen.getByTestId("ui-talad-016-ent-006-rate-percent") as HTMLInputElement).value).toBe("10");
    expect((screen.getByTestId("ui-talad-016-ent-006-start-date") as HTMLInputElement).value).toBe("2026-09-01");
    expect((screen.getByTestId("ui-talad-016-ent-006-end-date") as HTMLInputElement).value).toBe("");
  });

  it("state error · after a refusal the same form stays chosen, its fields drawn and filled, the message under the field", async () => {
    const action = vi.fn(async (_prev: unknown, fd: FormData) => {
      const values = { ...EMPTY_VALUES };
      for (const k of Object.keys(values) as (keyof typeof values)[]) values[k] = String(fd.get(k) ?? "");
      return { values, errors: { ratePercent: RATE_OUT_OF_RANGE } };
    });
    render(<PromotionForm initial={valuesOf(tenPercent)} action={action} find={noProducts} />);

    const rate = screen.getByTestId("ui-talad-016-ent-006-rate-percent");
    await userEvent.clear(rate);
    await userEvent.type(rate, "150");
    await userEvent.click(screen.getByTestId("ui-talad-016-save"));

    const message = await screen.findByText(RATE_OUT_OF_RANGE);
    expect(message.closest(".field")?.contains(screen.getByTestId("ui-talad-016-ent-006-rate-percent"))).toBe(true);
    expect((screen.getByTestId("ui-talad-016-ent-006-rate-percent") as HTMLInputElement).value).toBe("150");
    expect((screen.getByTestId("ui-talad-016-ent-006-type") as HTMLSelectElement).value).toBe("ITEM_PERCENT");
    expect((screen.getByTestId("ui-talad-016-ent-006-product-a") as HTMLInputElement).value).toBe("ส้มสายน้ำผึ้ง");
    expect((document.querySelector('input[name="productA"]') as HTMLInputElement).value).toBe("31");
    expect(screen.getByTestId("ui-talad-016-save").closest("form")?.noValidate).toBe(true);
  });

  it("a product is chosen from what the server finds; typing past it clears the choice", async () => {
    const find = vi.fn(async (term: string) => (term === "ส้ม" ? [orange, { id: 32, name: "ส้มเขียวหวาน" }] : []));
    render(<PromotionForm initial={EMPTY_VALUES} action={async () => undefined} find={find} />);
    await userEvent.selectOptions(screen.getByTestId("ui-talad-016-ent-006-type"), "ITEM_PERCENT");

    await userEvent.type(screen.getByTestId("ui-talad-016-ent-006-product-a"), "ส้ม");
    await userEvent.click(await screen.findByRole("button", { name: "ส้มเขียวหวาน" }));

    expect(find).toHaveBeenLastCalledWith("ส้ม");
    expect((screen.getByTestId("ui-talad-016-ent-006-product-a") as HTMLInputElement).value).toBe("ส้มเขียวหวาน");
    expect((document.querySelector('input[name="productA"]') as HTMLInputElement).value).toBe("32");
    expect(screen.queryByRole("listbox")).toBeNull();

    await userEvent.type(screen.getByTestId("ui-talad-016-ent-006-product-a"), "x");
    expect((document.querySelector('input[name="productA"]') as HTMLInputElement).value).toBe("");
    expect(await screen.findByText("ไม่พบสินค้า")).toBeTruthy();
  });

  it("a product search that fails says so instead of claiming nothing matched", async () => {
    render(<PromotionForm initial={EMPTY_VALUES} action={async () => undefined} find={async () => null} />);
    await userEvent.selectOptions(screen.getByTestId("ui-talad-016-ent-006-type"), "BUY_X_GET_Y");

    await userEvent.type(screen.getByTestId("ui-talad-016-ent-006-free-product"), "นม");

    expect(await screen.findByText("โหลดข้อมูลไม่สำเร็จ")).toBeTruthy();
  });

  it("state loading · save is off while saving; cancel goes back to UI-talad-015", async () => {
    let finish: () => void = () => {};
    const action = vi.fn(() => new Promise<undefined>((resolve) => (finish = () => resolve(undefined))));
    render(<PromotionForm initial={valuesOf(tenPercent)} action={action} find={noProducts} />);

    await userEvent.click(screen.getByTestId("ui-talad-016-save"));

    expect((screen.getByTestId("ui-talad-016-save") as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByTestId("ui-talad-016-cancel").getAttribute("href")).toBe("/promotions");
    finish();
  });
});

describe("FE-talad-026 · UI-talad-016 pages", () => {
  it("AC-talad-045 · the create page asks for UI-talad-016 first and opens empty", async () => {
    render(await NewPromotionPage());

    expect(requireScreen).toHaveBeenCalledWith("UI-talad-016");
    expect((screen.getByTestId("ui-talad-016-ent-006-name") as HTMLInputElement).value).toBe("");
  });

  it("AC-talad-045 · the edit page asks for UI-talad-016 first, then reads promotion 5 (API-026)", async () => {
    const fetch = apiAnswers(200, tenPercent);
    vi.stubGlobal("fetch", fetch);

    render(await EditPromotionPage({ params: Promise.resolve({ id: "5" }) }));

    expect(requireScreen).toHaveBeenCalledWith("UI-talad-016");
    expect(new URL(fetch.mock.calls[0][0]).pathname).toBe("/api/promotions/5");
    expect((screen.getByTestId("ui-talad-016-ent-006-rate-percent") as HTMLInputElement).value).toBe("10");
  });

  it("state unauthorized · a seller is sent away before the promotion is read", async () => {
    const fetch = apiAnswers(200, tenPercent);
    vi.stubGlobal("fetch", fetch);
    requireScreen.mockRejectedValueOnce(new Error("NEXT_REDIRECT /?denied=1"));

    await expect(EditPromotionPage({ params: Promise.resolve({ id: "5" }) })).rejects.toThrow("NEXT_REDIRECT /?denied=1");
    expect(fetch).not.toHaveBeenCalled();
  });

  it("a promotion gone or discontinued goes back to the list saying ไม่พบโปรโมชั่น", async () => {
    vi.stubGlobal("fetch", apiAnswers(404, { code: "PROMOTION_NOT_FOUND", errors: [] }));

    await expect(EditPromotionPage({ params: Promise.resolve({ id: "5" }) })).rejects.toThrow("NEXT_REDIRECT /promotions?gone=1");
  });
});

describe("FE-talad-026 · UI-talad-015 promotions page", () => {
  it("AC-talad-045 · asks for its own screen first, then searches with the term and page from the URL", async () => {
    const fetch = apiAnswers(200, pageOf([tenPercent]));
    vi.stubGlobal("fetch", fetch);

    await PromotionsPage({ searchParams: Promise.resolve({ q: "ส้ม", page: "2" }) });

    expect(requireScreen).toHaveBeenCalledWith("UI-talad-015");
    await waitFor(() => expect(fetch).toHaveBeenCalled());
    const url = new URL(fetch.mock.calls[0][0]);
    expect([url.pathname, url.searchParams.get("search"), url.searchParams.get("page")]).toEqual(["/api/promotions", "ส้ม", "2"]);
  });

  it("each row keyed by the promotion's id with its form, dates and status, and an edit icon first that opens UI-talad-016", async () => {
    render(await PromotionList({ loaded: Promise.resolve({ ok: true, page: pageOf([tenPercent, billPercent]) }), q: "" }));

    const names = screen.getAllByTestId("ui-talad-015-ent-006-name");
    expect(names.map((n) => n.closest("tr")?.getAttribute("data-row-key"))).toEqual(["5", "6"]);
    expect(names[0].getAttribute("title")).toBe("ส้มลด 10%");
    expect(screen.getAllByTestId("ui-talad-015-ent-006-type").map((t) => t.textContent)).toEqual(["ลด % ต่อสินค้า", "ลด % ทั้งบิล"]);
    expect(screen.getAllByTestId("ui-talad-015-ent-006-start-date")[0].textContent).toContain("2569");
    expect(screen.getAllByTestId("ui-talad-015-ent-006-end-date").map((t) => t.textContent)).toEqual(["ไม่มีวันสิ้นสุด", expect.stringContaining("31")]);
    expect(screen.getAllByTestId("ui-talad-015-ent-005-status")[0].textContent).toBe("ใช้อยู่");
    const edit = screen.getAllByTestId("ui-talad-015-edit-promo")[0];
    expect([edit.getAttribute("href"), edit.getAttribute("aria-label")]).toEqual(["/promotions/5", "แก้ไข"]);
    expect(edit.closest("td")).toBe(edit.closest("tr")?.firstElementChild);
    // ลบ is FE-talad-028's
    expect(screen.queryByTestId("ui-talad-015-discontinue")).toBeNull();
  });

  it("state empty · ไม่พบโปรโมชั่น with one create button, and the header draws none", async () => {
    const loaded = Promise.resolve({ ok: true as const, page: pageOf([]) });
    render(
      <>
        {await HeaderAddPromotion({ loaded })}
        {await PromotionList({ loaded, q: "ไม่มี" })}
      </>,
    );

    expect(screen.getByText("ไม่พบโปรโมชั่น")).toBeTruthy();
    expect(screen.getAllByTestId("ui-talad-015-add-promo")).toHaveLength(1);
    expect(screen.getByTestId("ui-talad-015-add-promo").getAttribute("href")).toBe("/promotions/new");
  });

  it("with rows the header carries the one create button", async () => {
    render(<>{await HeaderAddPromotion({ loaded: Promise.resolve({ ok: true, page: pageOf([tenPercent]) }) })}</>);

    expect(screen.getAllByTestId("ui-talad-015-add-promo")).toHaveLength(1);
  });

  it("state error · โหลดข้อมูลไม่สำเร็จ with a retry that keeps the term", async () => {
    render(await PromotionList({ loaded: Promise.resolve({ ok: false }), q: "ส้ม" }));

    expect(screen.getByRole("alert").textContent).toContain("โหลดข้อมูลไม่สำเร็จ");
    expect(screen.getByText("ลองใหม่").getAttribute("href")).toBe(`/promotions?q=${encodeURIComponent("ส้ม")}`);
  });

  it("state overflow · 21 promotions page at 20, with a link to the next page", async () => {
    const twenty = Array.from({ length: 20 }, (_, i) => ({ ...tenPercent, id: 100 + i }));
    render(await PromotionList({ loaded: Promise.resolve({ ok: true, page: pageOf(twenty, 21) }), q: "" }));

    expect(screen.getAllByTestId("ui-talad-015-ent-006-name")).toHaveLength(20);
    expect(screen.getByText("ถัดไป ›").getAttribute("href")).toBe("/promotions?page=2");
  });

  it("a promotion that was gone lands here saying ไม่พบโปรโมชั่น, on a param the frame's member notice does not answer", async () => {
    vi.stubGlobal("fetch", apiAnswers(200, pageOf([twentyPercent])));
    const members = await import("@/lib/member-form");
    const promotions = await import("@/lib/promotion-form");

    render(await PromotionsPage({ searchParams: Promise.resolve({ gone: "1" }) }));

    expect(screen.getByRole("alert").textContent).toBe("ไม่พบโปรโมชั่น");
    // (app)/layout.tsx shows ไม่พบสมาชิก on every page carrying the member param (FE-talad-014)
    expect(promotions.MISSING_PARAM).not.toBe(members.MISSING_PARAM);
  });

  it("a save that went through lands here saying บันทึกสำเร็จ", async () => {
    vi.stubGlobal("fetch", apiAnswers(200, pageOf([twentyPercent])));

    render(await PromotionsPage({ searchParams: Promise.resolve({ saved: "1" }) }));

    expect(screen.getByRole("status").textContent).toBe("บันทึกสำเร็จ");
  });
});
