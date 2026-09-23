"use client";

import { useRef, useState, useTransition } from "react";

type Props = { name: string; discontinue: () => Promise<{ ok: true } | { ok: false; message: string }> };

/**
 * UI-talad-015 row action ลบ (UC-talad-022) — an icon after แก้ไข in the row's first cell (UIC-001 view · edit ·
 * delete · UIC-002: named by the action's own name, ลบ); the data-testid is the wireframe's MCK-talad-015
 * control id. It asks first ("เปลี่ยนโปรเป็นเลิกใช้หลังยืนยัน"), and the dialog stays open with the reason
 * when the api refuses; when it goes through the list is redrawn without the row.
 */
export function DiscontinuePromotionButton({ name, discontinue }: Props) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [pending, startTransition] = useTransition();
  const [message, setMessage] = useState<string | null>(null);

  function confirm() {
    setMessage(null);
    startTransition(async () => {
      const result = await discontinue();
      if (result.ok) dialog.current?.close();
      else setMessage(result.message);
    });
  }

  return (
    <>
      <button
        type="button"
        className="iconbtn"
        aria-label="ลบ"
        disabled={pending}
        onClick={() => {
          setMessage(null);
          dialog.current?.showModal();
        }}
        data-testid="ui-talad-015-discontinue"
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <path d="M3 6h18" />
          <path d="M8 6V4h8v2" />
          <path d="M19 6l-1 14H6L5 6" />
        </svg>
      </button>

      <dialog ref={dialog} className="modal">
        <div className="modal-box">
          <p>{`ต้องการลบโปรโมชั่น ${name} หรือไม่?`}</p>
          <p className="muted">โปรจะเปลี่ยนเป็นเลิกใช้ บิลเก่ายังแสดงส่วนลดเดิม</p>
          {message ? (
            <div role="alert" className="toast">
              <div className="grow">{message}</div>
            </div>
          ) : null}
          <div className="modal-action">
            <button type="button" className="btn btn-error" disabled={pending} onClick={confirm}>
              {pending ? <span className="loading loading-spinner loading-sm" aria-hidden="true" /> : null}
              ลบ
            </button>
            <button type="button" className="btn" disabled={pending} onClick={() => dialog.current?.close()}>
              ยกเลิก
            </button>
          </div>
        </div>
      </dialog>
    </>
  );
}
