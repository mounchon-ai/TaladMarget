"use server";

import { revalidatePath } from "next/cache";
import { addOne, removeLine, setQty, type CartChange } from "@/lib/sales-api";

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
