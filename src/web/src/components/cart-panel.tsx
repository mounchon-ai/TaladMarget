"use client";

import { useRef, useState, useTransition } from "react";
import { formatBaht } from "@/lib/money";
import type { Cart, CartLine } from "@/lib/sales-api";

type Result = { ok: true } | { ok: false; message: string };

type Props = {
  cart: Cart;
  changeQty: (productId: number, qty: number) => Promise<Result>;
  remove: (productId: number) => Promise<Result>;
};

/**
 * UI-talad-002 zone cart (UC-talad-001). "+" and "−" change a line by one; "−" at 1 and ลบรายการ both ask
 * first (AC-talad-003). A refused change leaves the cart as it was and shows the rule's sentence
 * (AC-talad-069). The line shows no price field to edit (AC-talad-049). The total is before promotions —
 * those arrive with UC-talad-004.
 */
export function CartPanel({ cart, changeQty, remove }: Props) {
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

  function cancelRemove() {
    dialog.current?.close();
    setConfirming(null);
  }

  return (
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
            <div key={line.productId} className="cartline" data-row-key={line.productId}>
              <div className="cname">
                <div className="t" data-testid="ui-talad-002-ent-010-product" title={line.name}>
                  {line.name}
                </div>
                <span className="muted">{formatBaht(line.price)} / ชิ้น</span>
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
                <input className="input input-sm" readOnly value={line.qty} aria-label="จำนวน" data-testid="ui-talad-002-ent-010-qty" />
                <button
                  type="button"
                  className="btn btn-sm"
                  aria-label="เพิ่มจำนวน"
                  disabled={pending}
                  onClick={() => run(() => changeQty(line.productId, line.qty + 1))}
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

      <div className="sum">
        <span>ยอดรวม</span>
        <span data-testid="ui-talad-002-subtotal-after-item-promo">{formatBaht(cart.subtotal)}</span>
      </div>

      <dialog ref={dialog} className="modal" onCancel={cancelRemove}>
        <div className="modal-box">
          <p>{confirming ? `ต้องการลบ ${confirming.name} ออกจากตะกร้าหรือไม่?` : null}</p>
          <div className="modal-action">
            <button type="button" className="btn btn-error" onClick={confirmRemove}>
              ลบ
            </button>
            <button type="button" className="btn" onClick={cancelRemove}>
              ยกเลิก
            </button>
          </div>
        </div>
      </dialog>
    </div>
  );
}
