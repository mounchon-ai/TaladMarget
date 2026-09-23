"use client";

import { useState, useTransition } from "react";

type Props = {
  productId: number;
  action: (productId: number) => Promise<{ ok: true } | { ok: false; message: string }>;
};

/** UI-talad-002 action add-to-cart — a refusal (e.g. "… คงเหลือไม่พอ (เหลือ 0)") shows on the card itself. */
export function AddToCartButton({ productId, action }: Props) {
  const [pending, startTransition] = useTransition();
  const [message, setMessage] = useState<string | null>(null);

  function add() {
    setMessage(null);
    startTransition(async () => {
      const result = await action(productId);
      if (!result.ok) setMessage(result.message);
    });
  }

  return (
    <>
      <button type="button" className="btn btn-primary btn-sm" disabled={pending} onClick={add} data-testid="ui-talad-002-add-to-cart">
        หยิบใส่ตะกร้า
      </button>
      {message ? (
        <span role="alert" className="card-error">
          {message}
        </span>
      ) : null}
    </>
  );
}
