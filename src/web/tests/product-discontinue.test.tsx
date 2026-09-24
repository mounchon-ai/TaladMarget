import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { ProductDetail } from "@/lib/products-api";

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

const { discontinueAction } = await import("@/app/(app)/stock/[id]/actions");
const { default: ProductPage } = await import("@/app/(app)/stock/[id]/page");
const { DiscontinueProductZone } = await import("@/components/discontinue-product-zone");

// AC-talad-085's product: ส้มสายน้ำผึ้ง 45 baht, already sold in สมชาย's 90-baht bill
const orange: ProductDetail = {
  id: 1,
  name: "ส้มสายน้ำผึ้ง",
  barcode: "8850000000011",
  stockQty: 3,
  lowStockThreshold: 5,
  lowStock: true,
  price: 45,
  hasImage: false,
  priceHistory: { items: [], page: 1, pageSize: 20, total: 0 },
  adjustments: { items: [], page: 1, pageSize: 20, total: 0 },
};

function apiAnswers(status: number, body?: unknown) {
  return vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(
    async () => new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { "content-type": "application/json" } }),
  );
}

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

describe("FE-talad-022 · UI-talad-012 discontinue action (API-023)", () => {
  it.each(["AC-talad-085", "AC-talad-086"])("%s · the owner's ลบสินค้า goes to the api, then to the stock list redrawn without it", async () => {
    const fetch = apiAnswers(204);
    vi.stubGlobal("fetch", fetch);

    await expect(discontinueAction(1)).rejects.toThrow("NEXT_REDIRECT /stock");

    const [url, init] = fetch.mock.calls[0];
    expect([new URL(url).pathname, init?.method]).toEqual(["/api/products/1/discontinue", "POST"]);
    expect(new Headers(init?.headers).get("authorization")).toBe("Bearer jwt-owner");
    expect(revalidatePath).toHaveBeenCalledWith("/stock");
    expect(redirect).toHaveBeenCalledWith("/stock");
  });

  it("a product discontinued elsewhere since the page was drawn — the stock list says ไม่พบสินค้า", async () => {
    vi.stubGlobal("fetch", apiAnswers(404, { code: "PRODUCT_NOT_FOUND", errors: [] }));

    await expect(discontinueAction(1)).rejects.toThrow("NEXT_REDIRECT /stock?gone=1");
    expect(revalidatePath).toHaveBeenCalledWith("/stock");
  });

  it("ACL-020 · the api refuses anyone but the owner: the sentence of BR-talad-018 stays on the page, nothing is redrawn", async () => {
    vi.stubGlobal("fetch", apiAnswers(403));

    expect(await discontinueAction(1)).toEqual({ ok: false, message: "คุณไม่มีสิทธิ์เข้าถึงหน้านี้" });
    expect(redirect).not.toHaveBeenCalled();
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it("NFR-talad-005 · a session the api no longer accepts signs out", async () => {
    vi.stubGlobal("fetch", apiAnswers(401));

    await expect(discontinueAction(1)).rejects.toThrow("NEXT_REDIRECT /logout");
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it("the api cannot be reached: the page says so and stays", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("fetch failed"); }));

    expect(await discontinueAction(1)).toEqual({ ok: false, message: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่" });
    expect(redirect).not.toHaveBeenCalled();
  });
});

describe("FE-talad-022 · UI-talad-012 danger zone", () => {
  it("is drawn on the product page, last, with the wireframe's id", async () => {
    vi.stubGlobal("fetch", apiAnswers(200, orange));

    const { container } = render(await ProductPage({ params: Promise.resolve({ id: "1" }), searchParams: Promise.resolve({}) }));

    const button = screen.getByTestId("ui-talad-012-discontinue");
    expect(button.textContent).toBe("ลบสินค้า");
    const cards = container.querySelectorAll("[data-screen='UI-talad-012'] > .card");
    expect(cards[cards.length - 1].contains(button)).toBe(true);
  });

  it("asks first — ยกเลิก leaves the product as it was", async () => {
    const discontinue = vi.fn(async () => ({ ok: false as const, message: "" }));
    render(<DiscontinueProductZone name="ส้มสายน้ำผึ้ง" discontinue={discontinue} />);

    await userEvent.click(screen.getByTestId("ui-talad-012-discontinue"));
    const dialog = screen.getByRole("dialog", { hidden: true }) as HTMLDialogElement;
    expect(dialog.open).toBe(true);
    expect(dialog.textContent).toContain("ต้องการลบสินค้า ส้มสายน้ำผึ้ง หรือไม่?");
    await userEvent.click(screen.getByRole("button", { name: "ยกเลิก", hidden: true }));

    expect(dialog.open).toBe(false);
    expect(discontinue).not.toHaveBeenCalled();
  });

  it("ลบ after the question discontinues; a refusal comes back as a sentence on the page", async () => {
    const discontinue = vi.fn(async () => ({ ok: false as const, message: "คุณไม่มีสิทธิ์เข้าถึงหน้านี้" }));
    render(<DiscontinueProductZone name="ส้มสายน้ำผึ้ง" discontinue={discontinue} />);

    await userEvent.click(screen.getByTestId("ui-talad-012-discontinue"));
    await userEvent.click(screen.getByRole("button", { name: "ลบ", hidden: true }));

    expect(discontinue).toHaveBeenCalledTimes(1);
    expect((await screen.findByRole("alert")).textContent).toBe("คุณไม่มีสิทธิ์เข้าถึงหน้านี้");
    expect((screen.getByRole("dialog", { hidden: true }) as HTMLDialogElement).open).toBe(false);
  });
});
