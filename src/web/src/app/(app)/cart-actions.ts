"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { NO_MEMBER_FOUND } from "@/lib/member-form";
import { searchMembers, type Member } from "@/lib/members-api";
import { addOne, bindMember, removeLine, setQty, type CartChange, type MemberBinding } from "@/lib/sales-api";

// UI-talad-002 actions add-to-cart · increase-qty · decrease-qty · remove-line → API-005..007.
// The api decides (own cart only, stock, discontinued); a refusal comes back as its own sentence and the
// cart on screen stays as it was. Only the message crosses to the client (server-serialization).

type Result = { ok: true } | { ok: false; message: string };

async function settle(change: Promise<CartChange>): Promise<Result> {
  try {
    const result = await change;
    if (!result.ok) return { ok: false, message: result.message };
    revalidatePath("/");
    return { ok: true };
  } catch {
    return { ok: false, message: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่" };
  }
}

export async function addToCart(productId: number): Promise<Result> {
  return settle(addOne(productId));
}

export async function changeQty(productId: number, qty: number): Promise<Result> {
  return settle(setQty(productId, qty));
}

export async function removeFromCart(productId: number): Promise<Result> {
  return settle(removeLine(productId));
}

// UI-talad-002 zone member — actions search-member → API-012 and bind-member → API-008 (UC-talad-009).

const OFFLINE = "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่";

export type MemberSearch = { ok: true; members: Member[] } | { ok: false; message: string };

/** The first page of matches — a sale picks one person, and a full phone finds exactly one (BR-talad-004@v1). */
export async function findMembers(q: string): Promise<MemberSearch> {
  try {
    return { ok: true, members: (await searchMembers(q.trim())).items };
  } catch {
    return { ok: false, message: OFFLINE };
  }
}

/** Bind the chosen member to the caller's own cart; a member hidden since the search is not found (BR-talad-040@v2). */
export async function bindToCart(memberId: number): Promise<Result> {
  let outcome: MemberBinding;
  try {
    outcome = await bindMember(memberId);
  } catch {
    return { ok: false, message: OFFLINE };
  }
  // redirect() throws to do its work — outside the try, like sign-in
  if (!outcome.ok && outcome.code === "SIGNED_OUT") redirect("/logout");
  if (!outcome.ok) return { ok: false, message: outcome.code === "REFUSED" ? outcome.message : NO_MEMBER_FOUND };
  revalidatePath("/");
  return { ok: true };
}
