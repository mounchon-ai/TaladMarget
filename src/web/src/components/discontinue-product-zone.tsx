"use client";

import { useRef, useState, useTransition } from "react";

type Props = { name: string; discontinue: () => Promise<{ ok: false; message: string }> };

/**
 * UI-talad-012 zone danger (UC-talad-018) — last on the page, as the wireframe MCK-talad-013 orders its zones; the
 * data-testid is that wireframe's control id. ลบสินค้า asks first ("เปลี่ยนสินค้าเป็นเลิกขายหลังยืนยัน"), then the
 * action discontinues and goes to the stock list; a refusal stays here with its reason.
 */
export function DiscontinueProductZone({ name, discontinue }: Props) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [pending, startTransition] = useTransition();
  const [message, setMessage] = useState<string | null>(null);

  function confirm() {
    dialog.current?.close();
    setMessage(null);
    startTransition(async () => {
      // on success the action goes to the stock list and never returns here
      const result = await discontinue();
      if (result) setMessage(result.message);
    });
  }

  return (
    <div className="card dangerzone">
      <h2>ลบสินค้า</h2>
      <p className="muted">สินค้าจะเปลี่ยนเป็นเลิกขาย หายจากหน้าขายและหน้าสต็อก บิลเก่ายังแสดงสินค้าและราคาเดิมครบ</p>
      {message ? (
        <div role="alert" className="toast">
          <div className="grow">{message}</div>
        </div>
      ) : null}
      <div className="actions">
        <button
          type="button"
          className="btn btn-outline btn-error"
          disabled={pending}
          onClick={() => dialog.current?.showModal()}
          data-testid="ui-talad-012-discontinue"
        >
          {pending ? <span className="loading loading-spinner loading-sm" aria-hidden="true" /> : null}
          ลบสินค้า
        </button>
      </div>

      <dialog ref={dialog} className="modal">
        <div className="modal-box">
          <p>{`ต้องการลบสินค้า ${name} หรือไม่?`}</p>
          <div className="modal-action">
            <button type="button" className="btn btn-error" onClick={confirm}>
              ลบ
            </button>
            <button type="button" className="btn" onClick={() => dialog.current?.close()}>
              ยกเลิก
            </button>
          </div>
        </div>
      </dialog>
    </div>
  );
}
