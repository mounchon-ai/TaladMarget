"use client";

import { useRef, useState, useTransition } from "react";

type Props = { name: string; hide: () => Promise<{ ok: false; message: string }> };

/**
 * UI-talad-006 zone danger (UC-talad-010) — arrangement as mockups/UI-talad-006/default.html draws it
 * (reference); the data-testid is the wireframe's MCK-talad-007 control id. Drawn for the owner only by the
 * page; ลบสมาชิก asks first ("ซ่อนสมาชิกหลังยืนยัน"), then the action hides and goes back to the list.
 */
export function HideMemberZone({ name, hide }: Props) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [pending, startTransition] = useTransition();
  const [message, setMessage] = useState<string | null>(null);

  function confirm() {
    dialog.current?.close();
    setMessage(null);
    startTransition(async () => {
      // on success the action goes to the list and never returns here
      const result = await hide();
      if (result) setMessage(result.message);
    });
  }

  return (
    <div className="card dangerzone">
      <h2>ลบสมาชิก</h2>
      <p className="muted">สมาชิกจะถูกซ่อนจากรายการ ประวัติการขายที่ผูกไว้ยังคงอยู่</p>
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
          data-testid="ui-talad-006-hide-member"
        >
          ลบสมาชิก
        </button>
      </div>

      <dialog ref={dialog} className="modal">
        <div className="modal-box">
          <p>{`ต้องการลบสมาชิก ${name} หรือไม่?`}</p>
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
