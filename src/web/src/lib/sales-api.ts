import { apiFetch } from "./api-client";

// Shapes the api answers (FE-talad-005) — API-003 · API-004..007.

export type ProductCard = {
  id: number;
  name: string;
  barcode: string | null;
  price: number;
  stockQty: number;
  lowStock: boolean;
  hasImage: boolean;
};
export type ProductPage = { items: ProductCard[]; page: number; pageSize: number; total: number };

export type CartLine = {
  productId: number;
  name: string;
  price: number;
  qty: number;
  lineTotal: number;
  stockQty: number;
  lowStock: boolean;
};
/** ENT-009.member — who the cart is bound to (API-008 · FE-talad-011). */
export type CartMember = { id: number; name: string; phone: string; accumulatedAmount: number; status: string };
export type Cart = { id: number; status: string; lines: CartLine[]; subtotal: number; member?: CartMember | null };

/** A refused cart change — `message` is the rule's own sentence (BR-talad-007@v1 · BR-talad-037@v1). */
export type CartChange = { ok: true; cart: Cart } | { ok: false; message: string };

async function json<T>(response: Response): Promise<T> {
  if (!response.ok) throw new Error(`api answered ${response.status}`);
  return (await response.json()) as T;
}

export async function searchProducts(search: string, page: number): Promise<ProductPage> {
  const q = new URLSearchParams({ page: String(page) });
  if (search) q.set("search", search);
  return json<ProductPage>(await apiFetch(`/api/products?${q}`));
}

export async function getCart(): Promise<Cart> {
  return json<Cart>(await apiFetch("/api/cart"));
}

/** API-009 — a promotion as the pricing names it. */
export type PromotionRef = { id: number; name: string };
/** One of the tied promotions UI-talad-003 lists, with the baht it gives. */
export type PromotionChoice = PromotionRef & { discount: number };

/** One line as API-009 prices it — the discount fields are null while a tie waits for the staff. */
export type PricedLine = {
  productId: number;
  name: string;
  qty: number;
  unitPrice: number;
  lineGross: number;
  promotion: PromotionRef | null;
  itemPromoDiscount: number | null;
  lineNet: number | null;
};

/**
 * API-009 · UC-talad-004 — the caller's own cart priced now: each line with the promotion that took it, the whole-bill
 * discount, the member discount and what is to be paid. `needsChoice` names promotions that give exactly the same and
 * wait for the staff (BR-talad-029@v1) — every total is null until then.
 */
export type CartPricing = {
  lines: PricedLine[];
  promoDiscount: number | null;
  subtotal: number | null;
  billPromotion: PromotionRef | null;
  billRate: number | null;
  billDiscount: number | null;
  afterBill: number | null;
  member: CartMember | null;
  memberRate: number | null;
  memberDiscount: number | null;
  net: number | null;
  needsChoice: PromotionChoice[] | null;
};

/** API-009 · GET /api/cart/pricing — `choice` is the promotion the staff picked for each tied round, in order. */
export async function getCartPricing(choice: readonly number[] = []): Promise<CartPricing> {
  const q = new URLSearchParams();
  for (const c of choice) q.append("choice", String(c));
  const query = q.toString();
  return json<CartPricing>(await apiFetch(`/api/cart/pricing${query ? `?${query}` : ""}`));
}

async function change(response: Response): Promise<CartChange> {
  if (response.ok) return { ok: true, cart: (await response.json()) as Cart };
  if (response.status === 409 || response.status === 404) {
    const body = (await response.json().catch(() => null)) as { message?: string } | null;
    return { ok: false, message: body?.message ?? "ทำรายการไม่สำเร็จ กรุณาลองใหม่" };
  }
  throw new Error(`api answered ${response.status}`);
}

export async function addOne(productId: number): Promise<CartChange> {
  return change(await apiFetch("/api/cart/lines", { method: "POST", body: JSON.stringify({ productId }) }));
}

export async function setQty(productId: number, qty: number): Promise<CartChange> {
  return change(await apiFetch(`/api/cart/lines/${productId}`, { method: "PATCH", body: JSON.stringify({ qty }) }));
}

export async function removeLine(productId: number): Promise<CartChange> {
  return change(await apiFetch(`/api/cart/lines/${productId}`, { method: "DELETE" }));
}

/**
 * API-008 · PUT /api/cart/member. The api's refusal is told apart by `code`, never by its `message`:
 * MEMBER_NOT_FOUND carries a technical sentence, so the page says the declared one instead
 * (UI-talad-002 state "empty"); CART_NOT_OPEN's message is already the rule's own Thai sentence.
 */
export type MemberBinding =
  | { ok: true; cart: Cart }
  | { ok: false; code: "MEMBER_NOT_FOUND" | "SIGNED_OUT" }
  | { ok: false; code: "REFUSED"; message: string };

export async function bindMember(memberId: number): Promise<MemberBinding> {
  const response = await apiFetch("/api/cart/member", { method: "PUT", body: JSON.stringify({ memberId }) });
  if (response.ok) return { ok: true, cart: (await response.json()) as Cart };
  if (response.status === 401) return { ok: false, code: "SIGNED_OUT" };
  const body = (await response.json().catch(() => null)) as { code?: string; message?: string } | null;
  if (body?.code === "MEMBER_NOT_FOUND") return { ok: false, code: "MEMBER_NOT_FOUND" };
  if (body?.code === "CART_NOT_OPEN" && body.message) return { ok: false, code: "REFUSED", message: body.message };
  throw new Error(`PUT /api/cart/member answered ${response.status}`);
}
