"use client";

import { useActionState, useRef, useState } from "react";
import { EMPTY_VALUES, REASONS, newRequestKey, type AdjustFormState } from "@/lib/stock-adjustment-form";

type Action = (prev: AdjustFormState, formData: FormData) => Promise<AdjustFormState>;
type Props = { current: number; action: Action };
/** Each answer is numbered, so the form is redrawn with what was typed even when two answers carry the same values. */
type Answered = (NonNullable<AdjustFormState> & { round: number }) | undefined;

/**
 * UI-talad-012 action ปรับสต็อก → UI-talad-014 ปรับสต็อก, a window over the detail (kind modal) — the wireframes
 * MCK-talad-013 · 014 are the only pictures; every data-testid is theirs. Opening it mints a new form key
 * ("เปิดฟอร์มปรับสต็อกพร้อมคีย์กันบันทึกซ้ำใหม่"), so a second delivery is a second form (AC-talad-081) while a resend
 * of this one is refused (AC-talad-079 · 080). Save closes the window and the detail is redrawn with the new stock
 * and its row; cancel closes it without saving.
 */
export function AdjustStockDialog({ current, action }: Props) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [requestKey, setRequestKey] = useState<string | null>(null);

  return (
    <>
      <button
        type="button"
        className="btn"
        onClick={() => {
          setRequestKey(newRequestKey());
          dialog.current?.showModal();
        }}
        data-testid="ui-talad-012-adjust-stock"
      >
        ปรับสต็อก
      </button>

      <dialog ref={dialog} className="modal" aria-labelledby="adjust-stock-title">
        {requestKey ? (
          // a new key is a new form — what the last one typed or was told does not carry over
          <AdjustStockForm key={requestKey} requestKey={requestKey} current={current} action={action} close={() => dialog.current?.close()} />
        ) : null}
      </dialog>
    </>
  );
}

function AdjustStockForm({ requestKey, current, action, close }: Props & { requestKey: string; close: () => void }) {
  const [state, formAction, pending] = useActionState<Answered, FormData>(async (prev, formData) => {
    const next = await action(prev, formData);
    if (next?.saved) close();
    return next ? { ...next, round: (prev?.round ?? 0) + 1 } : undefined;
  }, undefined);
  const values = state?.values ?? EMPTY_VALUES;
  const errors = state?.errors ?? {};

  return (
    // redrawn with what was typed after each answer (state "error": ข้อมูลที่กรอกค้างไว้ไม่หาย) — React clears a form
    // once its action has run
    <form action={formAction} noValidate className="modal-box" key={state?.round ?? 0}>
      <h2 id="adjust-stock-title">ปรับสต็อก</h2>
      <input type="hidden" name="requestKey" value={requestKey} />

      <div className="field">
        <span className="lbl">คงเหลือปัจจุบัน</span>
        <div className="value" data-testid="ui-talad-014-ent-001-stock-qty">
          {current.toLocaleString("th-TH")}
        </div>
      </div>

      <label className={errors.reason ? "field invalid" : "field"}>
        <span className="lbl">
          เหตุผล <span className="req">*</span>
        </span>
        <select
          name="reason"
          defaultValue={values.reason}
          className="select"
          aria-invalid={errors.reason ? true : undefined}
          data-testid="ui-talad-014-ent-003-reason"
        >
          <option value="">เลือกเหตุผล</option>
          {REASONS.map((r) => (
            <option key={r.code} value={r.code}>
              {r.label}
            </option>
          ))}
        </select>
        {errors.reason ? <span className="err">{errors.reason}</span> : null}
      </label>

      <label className={errors.quantity ? "field invalid" : "field"}>
        <span className="lbl">จำนวน +/−</span>
        <input
          type="text"
          name="quantity"
          inputMode="numeric"
          defaultValue={values.quantity}
          autoComplete="off"
          className="input"
          aria-invalid={errors.quantity ? true : undefined}
          data-testid="ui-talad-014-ent-003-quantity-delta"
        />
        {errors.quantity ? <span className="err">{errors.quantity}</span> : null}
      </label>

      <label className={errors.countedQty ? "field invalid" : "field"}>
        <span className="lbl">จำนวนที่นับได้</span>
        <input
          type="text"
          name="countedQty"
          inputMode="numeric"
          defaultValue={values.countedQty}
          autoComplete="off"
          className="input"
          aria-invalid={errors.countedQty ? true : undefined}
          data-testid="ui-talad-014-ent-003-counted-qty"
        />
        {errors.countedQty ? <span className="err">{errors.countedQty}</span> : null}
      </label>

      <label className="field">
        <span className="lbl">หมายเหตุ</span>
        <textarea name="note" rows={3} defaultValue={values.note} className="textarea" data-testid="ui-talad-014-ent-003-note" />
      </label>

      {state?.message ? (
        <div role="alert" className="alert">
          {state.message}
        </div>
      ) : null}

      <div className="modal-action">
        {/* state "loading" — save is off while saving, so it cannot be pressed twice */}
        <button type="submit" className="btn btn-primary" disabled={pending} data-testid="ui-talad-014-save">
          {pending ? <span className="loading loading-spinner loading-sm" aria-hidden="true" /> : null}
          บันทึก
        </button>
        <button type="button" className="btn" disabled={pending} onClick={close} data-testid="ui-talad-014-cancel">
          ยกเลิก
        </button>
      </div>
    </form>
  );
}
