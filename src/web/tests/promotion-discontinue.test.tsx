import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { Promotion } from "@/lib/promotions-api";

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

const { discontinuePromotionAction } = await import("@/app/(app)/promotions/actions");
const { PromotionList } = await import("@/components/promotion-list");
const { DiscontinuePromotionButton } = await import("@/components/discontinue-promotion-button");

// AC-talad-064 · AC-talad-088's promotion: ส้มสายน้ำผึ้ง 10% off
const tenPercent: Promotion = {
  id: 5,
  status: "Active",
  versionId: 40,
  name: "ส้มลด 10%",
  type: "ITEM_PERCENT",
  productA: { id: 31, name: "ส้มสายน้ำผึ้ง" },
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
const billPercent: Promotion = { ...tenPercent, id: 6, versionId: 42, name: "ลดทั้งบิล 5%", type: "BILL_PERCENT", productA: null, ratePercent: 5, minSubtotal: 500 };
const pageOf = (items: Promotion[]) => ({ items, page: 1, pageSize: 20, total: items.length });

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

describe("FE-talad-028 · UI-talad-015 discontinue action (API-029)", () => {
  it.each(["AC-talad-064", "AC-talad-088"])("%s · the owner's ลบ goes to the api and the list is redrawn without it", async () => {
    const fetch = apiAnswers(204);
    vi.stubGlobal("fetch", fetch);

    expect(await discontinuePromotionAction(5)).toEqual({ ok: true });

    const [url, init] = fetch.mock.calls[0];
    expect([new URL(url).pathname, init?.method]).toEqual(["/api/promotions/5/discontinue", "POST"]);
    expect(new Headers(init?.headers).get("authorization")).toBe("Bearer jwt-owner");
    expect(revalidatePath).toHaveBeenCalledWith("/promotions");
  });

  it("one discontinued elsewhere since the list was drawn: the list is redrawn and says ไม่พบโปรโมชั่น", async () => {
    vi.stubGlobal("fetch", apiAnswers(404, { code: "PROMOTION_NOT_FOUND", errors: [] }));

    expect(await discontinuePromotionAction(5)).toEqual({ ok: false, message: "ไม่พบโปรโมชั่น" });
    expect(revalidatePath).toHaveBeenCalledWith("/promotions");
  });

  it("a seller refused by the api gets the no-permission sentence and nothing is redrawn", async () => {
    vi.stubGlobal("fetch", apiAnswers(403));

    expect(await discontinuePromotionAction(5)).toEqual({ ok: false, message: "คุณไม่มีสิทธิ์เข้าถึงหน้านี้" });
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it("an expired session signs out; no server asks to try again", async () => {
    vi.stubGlobal("fetch", apiAnswers(401));
    await expect(discontinuePromotionAction(5)).rejects.toThrow("NEXT_REDIRECT /logout");

    vi.stubGlobal("fetch", vi.fn(async () => Promise.reject(new Error("ECONNREFUSED"))));
    expect(await discontinuePromotionAction(5)).toEqual({ ok: false, message: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่" });
  });
});

describe("FE-talad-028 · UI-talad-015 ลบ in each row", () => {
  it("UIC-001 · UIC-002 — ลบ is an icon named ลบ in the first cell, after แก้ไข, bound to its own row's promotion", async () => {
    const discontinue = vi.fn(async (id: number) => ({ ok: true as const, id }));
    render(await PromotionList({ loaded: Promise.resolve({ ok: true, page: pageOf([tenPercent, billPercent]) }), q: "", discontinue }));

    const buttons = screen.getAllByTestId("ui-talad-015-discontinue");
    expect(buttons.map((b) => b.closest("tr")?.getAttribute("data-row-key"))).toEqual(["5", "6"]);
    const cell = buttons[1].closest("td")!;
    expect(cell).toBe(buttons[1].closest("tr")?.firstElementChild);
    expect([...cell.querySelectorAll("[data-testid]")].map((e) => e.getAttribute("data-testid"))).toEqual([
      "ui-talad-015-edit-promo",
      "ui-talad-015-discontinue",
    ]);
    expect(buttons[1].getAttribute("aria-label")).toBe("ลบ");

    await userEvent.click(buttons[1]);
    // the dialog's own ลบ — the one in this row's cell
    await userEvent.click(within(cell.querySelector("dialog")!).getByText("ลบ"));

    expect(discontinue).toHaveBeenCalledWith(6);
  });

  it("ลบ asks first, naming the promotion; ยกเลิก leaves it, ลบ in the dialog discontinues it and closes", async () => {
    const discontinue = vi.fn(async () => ({ ok: true as const }));
    render(<DiscontinuePromotionButton name="ส้มลด 10%" discontinue={discontinue} />);

    await userEvent.click(screen.getByTestId("ui-talad-015-discontinue"));
    const dialog = document.querySelector("dialog")!;
    expect(dialog.open).toBe(true);
    expect(within(dialog).getByText("ต้องการลบโปรโมชั่น ส้มลด 10% หรือไม่?")).toBeTruthy();
    expect(within(dialog).getByText("โปรจะเปลี่ยนเป็นเลิกใช้ บิลเก่ายังแสดงส่วนลดเดิม")).toBeTruthy();

    await userEvent.click(within(dialog).getByText("ยกเลิก"));
    expect(dialog.open).toBe(false);
    expect(discontinue).not.toHaveBeenCalled();

    await userEvent.click(screen.getByTestId("ui-talad-015-discontinue"));
    await userEvent.click(within(dialog).getByText("ลบ"));

    expect(discontinue).toHaveBeenCalledTimes(1);
    expect(dialog.open).toBe(false);
  });

  it("a refusal keeps the dialog open with the reason", async () => {
    render(<DiscontinuePromotionButton name="ส้มลด 10%" discontinue={async () => ({ ok: false, message: "คุณไม่มีสิทธิ์เข้าถึงหน้านี้" })} />);

    await userEvent.click(screen.getByTestId("ui-talad-015-discontinue"));
    const dialog = document.querySelector("dialog")!;
    await userEvent.click(within(dialog).getByText("ลบ"));

    expect((await within(dialog).findByRole("alert")).textContent).toBe("คุณไม่มีสิทธิ์เข้าถึงหน้านี้");
    expect(dialog.open).toBe(true);
  });

  it("state loading · both dialog buttons are off while the api answers", async () => {
    let finish: () => void = () => {};
    const discontinue = vi.fn(() => new Promise<{ ok: true }>((resolve) => (finish = () => resolve({ ok: true }))));
    render(<DiscontinuePromotionButton name="ส้มลด 10%" discontinue={discontinue} />);

    await userEvent.click(screen.getByTestId("ui-talad-015-discontinue"));
    const dialog = document.querySelector("dialog")!;
    await userEvent.click(within(dialog).getByText("ลบ"));

    expect([...dialog.querySelectorAll("button")].every((b) => b.disabled)).toBe(true);
    finish();
  });
});
