"use client";

import { useRef, useState, useTransition, type ReactNode } from "react";
import { formatBaht } from "@/lib/money";
import type { Cart, CartLine, CartPricing } from "@/lib/sales-api";

type Result = { ok: true } | { ok: false; message: string };

type Props = {
  cart: Cart;
  /** API-009 — null when it could not be read; the totals then show — and say so. */
  pricing: CartPricing | null;
  changeQty: (productId: number, qty: number) => Promise<Result>;
  remove: (productId: number) => Promise<Result>;
  /** The till's command — ชำระเงิน (FE-talad-034), drawn under ยอดที่ต้องชำระ as the mockup places it. */
  checkout?: ReactNode;
};

/**
 * UI-talad-002 zone cart (UC-talad-001). "+" and "−" change a line by one; "−" at 1 and ลบรายการ both ask
 * first (AC-talad-003). A refused change leaves the cart as it was and shows the rule's sentence
 * (AC-talad-069). The line shows no price field to edit (AC-talad-049).
 *
 * Zone totals (UC-talad-004 · FE-talad-032) — the till card below it, as mockups/UI-talad-002 (reference) draws it:
 * each line carries the name of the promotion that took it and what it gave that line (BR-talad-016@v2 — a set
 * promotion shows its part under each of its lines), then ยอดรวมหลังโปรระดับสินค้า · ส่วนลดทั้งบิล · ส่วนลดสมาชิก ·
 * ยอดที่ต้องชำระ from API-009. While promotions give exactly the same and wait for the staff (BR-talad-029@v1) every
 * total is — : UI-talad-003 asks at ชำระเงิน, which is FE-talad-034's.
 */
export function CartPanel({ cart, pricing, changeQty, remove, checkout }: Props) {
  const [pending, startTransition] = useTransition();
  const [message, setMessage] = useState<string | null>(null);
  const [confirming, setConfirming] = useState<CartLine | null>(null);
  const dialog = useRef<HTMLDialogElement>(null);

  function run(change: () => Promise<Result>) {
    setMessage(null);
    startTransition(async () => {
      const result = await change();
      if (!result.ok) setMessage(result.message);
    });
  }

  function askToRemove(line: CartLine) {
    setConfirming(line);
    dialog.current?.showModal();
  }

  function decrease(line: CartLine) {
    if (line.qty === 1) askToRemove(line);
    else run(() => changeQty(line.productId, line.qty - 1));
  }

  function confirmRemove() {
    const line = confirming;
    dialog.current?.close();
    setConfirming(null);
    if (line) run(() => remove(line.productId));
  }

  const pricedLine = (productId: number) =>
    pricing?.lines.find((l) => l.productId === productId);
  const amount = (value: number | null | undefined) =>
    value == null ? "—" : formatBaht(value);
  const off = (value: number | null | undefined) =>
    value == null ? "—" : value === 0 ? formatBaht(0) : `−${formatBaht(value)}`;

  function cancelRemove() {
    dialog.current?.close();
    setConfirming(null);
  }

  return (
    <>
      <div className="card">
        <h2>
          ตะกร้า <span className="muted">{cart.lines.length} รายการ</span>
        </h2>

        {message ? (
          <div role="alert" className="toast">
            <div className="grow">{message}</div>
          </div>
        ) : null}

        {cart.lines.length === 0 ? (
          <p className="muted">ยังไม่มีสินค้าในตะกร้า</p>
        ) : (
          <div className="cartlines">
            {cart.lines.map((line) => (
              <div
                key={line.productId}
                className="cartline"
                data-row-key={line.productId}
              >
                <div className="cname">
                  <div
                    className="t"
                    data-testid="ui-talad-002-ent-010-product"
                    title={line.name}
                  >
                    {line.name}
                  </div>
                  <span className="muted">{formatBaht(line.price)} / ชิ้น</span>
                  <PromotionTag line={pricedLine(line.productId)} />
                </div>
                <div className="qty">
                  <button
                    type="button"
                    className="btn btn-sm"
                    aria-label="ลดจำนวน"
                    disabled={pending}
                    onClick={() => decrease(line)}
                    data-testid="ui-talad-002-decrease-qty"
                  >
                    −
                  </button>
                  <input
                    className="input input-sm"
                    readOnly
                    value={line.qty}
                    aria-label="จำนวน"
                    data-testid="ui-talad-002-ent-010-qty"
                  />
                  <button
                    type="button"
                    className="btn btn-sm"
                    aria-label="เพิ่มจำนวน"
                    disabled={pending}
                    onClick={() =>
                      run(() => changeQty(line.productId, line.qty + 1))
                    }
                    data-testid="ui-talad-002-increase-qty"
                  >
                    +
                  </button>
                </div>
                <button
                  type="button"
                  className="btn btn-ghost btn-sm"
                  disabled={pending}
                  onClick={() => askToRemove(line)}
                  data-testid="ui-talad-002-remove-line"
                >
                  ลบรายการ
                </button>
              </div>
            ))}
          </div>
        )}

        <dialog ref={dialog} className="modal" onCancel={cancelRemove}>
          <div className="modal-box">
            <p>
              {confirming
                ? `ต้องการลบ ${confirming.name} ออกจากตะกร้าหรือไม่?`
                : null}
            </p>
            <div className="modal-action">
              <button
                type="button"
                className="btn btn-error"
                onClick={confirmRemove}
              >
                ลบ
              </button>
              <button type="button" className="btn" onClick={cancelRemove}>
                ยกเลิก
              </button>
            </div>
          </div>
        </dialog>
      </div>

      <div className="card till">
        <h2>สรุปยอด</h2>
        {pricing ? null : (
          <div role="alert" className="toast">
            <div className="grow">เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่</div>
          </div>
        )}
        <div className="sum">
          <span>ยอดรวมหลังโปรระดับสินค้า</span>
          <span data-testid="ui-talad-002-subtotal-after-item-promo">
            {amount(pricing?.subtotal)}
          </span>
        </div>
        <div className="sum">
          <span>ส่วนลดทั้งบิล</span>
          <span data-testid="ui-talad-002-bill-discount">
            {off(pricing?.billDiscount)}
          </span>
        </div>
        <div className="sum">
          <span>ส่วนลดสมาชิก</span>
          <span data-testid="ui-talad-002-member-discount">
            {off(pricing?.memberDiscount)}
          </span>
        </div>
        <div className="due">
          <span>ยอดที่ต้องชำระ</span>
          <span className="v" data-testid="ui-talad-002-amount-due">
            {amount(pricing?.net)}
          </span>
        </div>
        {checkout}
      </div>
    </>
  );
}

/** The promotion that took this line, and the baht it gave the line (BR-talad-016@v2). */
function PromotionTag({
  line,
}: {
  line: CartPricing["lines"][number] | undefined;
}) {
  if (!line?.promotion) return null;
  return (
    <div className="promoline">
      <span className="tag promo" data-testid="ui-talad-002-ent-006-name">
        {line.promotion.name}
      </span>
      {line.itemPromoDiscount ? (
        <span className="muted">−{formatBaht(line.itemPromoDiscount)}</span>
      ) : null}
    </div>
  );
}
