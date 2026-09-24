import type { Cart, CartPricing } from "@/lib/sales-api";

/** API-009 for a cart no promotion, whole-bill discount or member discount touches — every line at its full price. */
export function noPromotions(cart: Cart): CartPricing {
  return {
    lines: cart.lines.map((l) => ({
      productId: l.productId,
      name: l.name,
      qty: l.qty,
      unitPrice: l.price,
      lineGross: l.lineTotal,
      promotion: null,
      itemPromoDiscount: 0,
      lineNet: l.lineTotal,
    })),
    promoDiscount: 0,
    subtotal: cart.subtotal,
    billPromotion: null,
    billRate: 0,
    billDiscount: 0,
    afterBill: cart.subtotal,
    member: null,
    memberRate: 0,
    memberDiscount: 0,
    net: cart.subtotal,
    needsChoice: null,
  };
}
