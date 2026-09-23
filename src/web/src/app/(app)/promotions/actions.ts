"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import type { ProductOption } from "@/components/product-picker";
import { DENIED_MESSAGE } from "@/lib/denied";
import {
  MISSING_PARAM,
  NO_PROMOTION,
  PROMOTIONS_PATH,
  SAVED_PARAM,
  readValues,
  termsOf,
  type PromotionFormState,
} from "@/lib/promotion-form";
import { discontinuePromotion, savePromotion, type PromotionDiscontinuing, type PromotionSave } from "@/lib/promotions-api";
import { searchProducts } from "@/lib/sales-api";

// UI-talad-016 action "save" → API-027 (id null: a new promotion) or API-028 (an edit: a new version in force
// from now, BR-talad-035@v1). The page checks nothing itself — every rule is the domain's — and on a refusal
// nothing is saved, each message sits under its field and the typed values stay (state "error").
export async function savePromotionAction(id: number | null, _prev: PromotionFormState, formData: FormData): Promise<PromotionFormState> {
  const values = readValues(formData);

  let outcome: PromotionSave;
  try {
    outcome = await savePromotion(id, termsOf(values));
  } catch {
    return { values, errors: {}, message: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่" };
  }
  // redirect() throws to do its work — outside the try
  if ("refused" in outcome) {
    if (outcome.refused === "signedOut") redirect("/logout");
    if (outcome.refused === "gone") redirect(`${PROMOTIONS_PATH}?${MISSING_PARAM}=1`);
    return { values, errors: {}, message: DENIED_MESSAGE };
  }
  if (!outcome.ok) {
    return Object.keys(outcome.errors).length > 0
      ? { values, errors: outcome.errors }
      : { values, errors: {}, message: "บันทึกไม่สำเร็จ กรุณาลองใหม่" };
  }
  revalidatePath(PROMOTIONS_PATH);
  redirect(`${PROMOTIONS_PATH}?${SAVED_PARAM}=1`);
}

/**
 * UI-talad-016 state "overflow" — the product fields search at the server, never load the catalog: the first
 * 20 ACTIVE products matching the term (API-003, the sales page's own search). The browser cannot ask the api
 * itself because the session cookie is httpOnly. null when the api could not be asked — the picker says so,
 * rather than claiming nothing matched.
 */
export async function findProducts(term: string): Promise<ProductOption[] | null> {
  try {
    const page = await searchProducts(term.trim(), 1);
    return page.items.map((p) => ({ id: p.id, name: p.name }));
  } catch {
    return null;
  }
}

/** What the list shows after ลบ — nothing when it went through (the row is gone), a sentence when it did not. */
export type DiscontinueResult = { ok: true } | { ok: false; message: string };

// UI-talad-015 action "discontinue" → API-029, after the owner confirmed (UC-talad-022). Its destination is
// the same page: the list is redrawn without the promotion. One discontinued elsewhere since the list was
// drawn is gone too, so the list is redrawn and says ไม่พบโปรโมชั่น.
export async function discontinuePromotionAction(id: number): Promise<DiscontinueResult> {
  let outcome: PromotionDiscontinuing;
  try {
    outcome = await discontinuePromotion(id);
  } catch {
    return { ok: false, message: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่" };
  }
  if (outcome === "signedOut") redirect("/logout");
  if (outcome === "forbidden") return { ok: false, message: DENIED_MESSAGE };
  revalidatePath(PROMOTIONS_PATH);
  return outcome === "gone" ? { ok: false, message: NO_PROMOTION } : { ok: true };
}
