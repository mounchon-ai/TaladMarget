"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { DENIED_MESSAGE } from "@/lib/denied";
import { discontinueProduct, repriceProduct, type ProductDiscontinuing, type Repricing } from "@/lib/products-api";

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
