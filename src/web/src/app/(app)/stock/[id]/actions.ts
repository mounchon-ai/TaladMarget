"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { DENIED_MESSAGE } from "@/lib/denied";
import { adjustStock, discontinueProduct, repriceProduct, type ProductDiscontinuing, type Repricing, type StockAdjusting } from "@/lib/products-api";
import { EMPTY_VALUES, numberOrNull, readAdjustValues, type AdjustFormState } from "@/lib/stock-adjustment-form";

/** UI-talad-013 after a save — what was typed, and why it did not go through; `saved` closes the window. */
export type PriceFormState = { typed: string; error?: string; message?: string; saved?: boolean } | undefined;

// UI-talad-013 action "save" → API-022 from the stock screen (source STOCK_SCREEN). The page checks nothing
// itself — the rule is the domain's; a refused price leaves the one in force and its sentence sits under the
// field (state "error"). A product discontinued while the window was open: say so and close — the detail is
// gone too, so the stock list says ไม่พบสินค้า.
export async function repriceAction(id: number, _prev: PriceFormState, formData: FormData): Promise<PriceFormState> {
  const typed = String(formData.get("price") ?? "").trim();
  const number = typed === "" ? Number.NaN : Number(typed);

  let outcome: Repricing;
  try {
    outcome = await repriceProduct(id, Number.isFinite(number) ? number : null, "STOCK_SCREEN");
  } catch {
    return { typed, message: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่" };
  }
  if ("refused" in outcome) {
    if (outcome.refused === "signedOut") redirect("/logout");
    if (outcome.refused === "gone") redirect("/stock?gone=1");
    return { typed, message: DENIED_MESSAGE };
  }
  if (!outcome.ok) return { typed, error: outcome.error };
  revalidatePath(`/stock/${id}`);
  return { typed: "", saved: true };
}

/** What the detail shows when ลบสินค้า did not go through — when it did, the owner is taken to the stock list. */
export type DiscontinueResult = { ok: false; message: string };

// UI-talad-012 action "discontinue" → API-023, after the owner confirmed (UC-talad-018). Its destination is the
// stock list, redrawn without the product (AC-talad-085 · 086). One discontinued elsewhere since the page was
// drawn is gone too, so the stock list says ไม่พบสินค้า.
export async function discontinueAction(id: number): Promise<DiscontinueResult> {
  let outcome: ProductDiscontinuing;
  try {
    outcome = await discontinueProduct(id);
  } catch {
    return { ok: false, message: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่" };
  }
  if (outcome === "signedOut") redirect("/logout");
  if (outcome === "forbidden") return { ok: false, message: DENIED_MESSAGE };
  revalidatePath("/stock");
  redirect(outcome === "gone" ? "/stock?gone=1" : "/stock");
}

// UI-talad-014 action "save" → API-024 with the key the form minted when it opened (BR-talad-041@v1). The page checks
// nothing itself — the rules are the domain's: a refusal comes back under its field and the form keeps what was
// typed (state "error"). A resend of a form already saved says so and adjusts nothing, and the detail is redrawn so
// the stock shown is the one saved. Only the number the chosen reason uses is sent: the change for รับของเข้า ·
// ของเน่า/เสีย, what was counted for นับสต็อกใหม่.
export async function adjustStockAction(id: number, _prev: AdjustFormState, formData: FormData): Promise<AdjustFormState> {
  const values = readAdjustValues(formData);
  const recount = values.reason === "RECOUNT";

  let outcome: StockAdjusting;
  try {
    outcome = await adjustStock(id, {
      reason: values.reason === "" ? null : values.reason,
      quantity: recount ? null : numberOrNull(values.quantity),
      countedQty: recount ? numberOrNull(values.countedQty) : null,
      note: values.note.trim() === "" ? null : values.note,
      requestKey: String(formData.get("requestKey") ?? ""),
    });
  } catch {
    return { values, errors: {}, message: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่" };
  }
  if ("refused" in outcome) {
    if (outcome.refused === "signedOut") redirect("/logout");
    if (outcome.refused === "gone") redirect("/stock?gone=1");
    return { values, errors: {}, message: DENIED_MESSAGE };
  }
  if ("duplicate" in outcome) {
    revalidatePath(`/stock/${id}`);
    return { values, errors: {}, message: outcome.duplicate };
  }
  if (!outcome.ok) {
    return outcome.field === "requestKey"
      ? { values, errors: {}, message: outcome.error }
      : { values, errors: { [outcome.field]: outcome.error } };
  }
  revalidatePath(`/stock/${id}`);
  return { values: EMPTY_VALUES, errors: {}, saved: true };
}
