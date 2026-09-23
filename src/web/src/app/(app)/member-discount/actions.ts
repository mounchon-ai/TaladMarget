"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { DENIED_MESSAGE } from "@/lib/denied";
import { setMemberDiscount, type MemberDiscount, type MemberDiscountChange } from "@/lib/settings-api";

/** UI-talad-017 after a save — what is in force now, what was typed, and why it did not go through. */
export type DiscountFormState = { current: MemberDiscount; typed: string; error?: string; message?: string } | undefined;

// UI-talad-017 action "save" → API-031. A refused % leaves the one in force (state "error"); the rule's
// sentence sits under the field. The page does not check the range itself — BR-talad-010@v1 is the domain's.
export async function saveMemberDiscount(initial: MemberDiscount, prev: DiscountFormState, formData: FormData): Promise<DiscountFormState> {
  // what is in force: the last save that went through on this page, or what the page opened with
  const current = prev?.current ?? initial;
  const typed = String(formData.get("ratePercent") ?? "").trim();
  const rate = typed === "" ? null : Number(typed);

  let outcome: MemberDiscountChange;
  try {
    outcome = await setMemberDiscount(rate === null || Number.isNaN(rate) ? null : rate);
  } catch {
    return { current, typed, message: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่" };
  }
  if ("refused" in outcome && outcome.refused === "signedOut") redirect("/logout");
  if ("refused" in outcome) return { current, typed, message: DENIED_MESSAGE };
  if (!outcome.ok) return { current, typed, error: outcome.error };
  revalidatePath("/member-discount");
  return { current: outcome.current, typed: String(outcome.current.ratePercent) };
}
