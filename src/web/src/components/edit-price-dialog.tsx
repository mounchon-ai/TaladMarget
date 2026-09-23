"use client";

import { useActionState, useRef } from "react";
import { formatBaht } from "@/lib/money";

type State = { typed: string; error?: string; message?: string; saved?: boolean } | undefined;
type Props = { current: number; action: (prev: State, formData: FormData) => Promise<State> };

/**
 * UI-talad-012 action แก้ราคา → UI-talad-013 แก้ราคาสินค้า, a window over the detail (kind modal) — the wireframes
 * MCK-talad-012 · 013 are the only pictures; every data-testid is theirs. Save closes the window and the detail
 * is redrawn with the new price and its history row; cancel closes it. noValidate: the domain's sentence, not
 * the browser's bubble, answers a price not above 0 (state "error").
 */
export function EditPriceDialog({ current, action }: Props) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [state, formAction, pending] = useActionState(async (prev: State, formData: FormData) => {
    const next = await action(prev, formData);
    if (next?.saved) dialog.current?.close();
    return next;
  }, undefined);
  const typed = state?.saved ? "" : (state?.typed ?? "");

  return (
    <>
      <button type="button" className="btn" onClick={() => dialog.current?.showModal()} data-testid="ui-talad-012-edit-price">
        แก้ราคา
      </button>

      <dialog ref={dialog} className="modal" aria-labelledby="edit-price-title">
        <form action={formAction} noValidate className="modal-box">
          <h2 id="edit-price-title">แก้ราคาสินค้า</h2>

          <div className="field">
            <span className="lbl">ราคาปัจจุบัน</span>
            <div className="value" data-testid="ui-talad-013-current-price">
              {formatBaht(current)}
            </div>
          </div>

          <label className={state?.error ? "field invalid" : "field"}>
            <span className="lbl">
              ราคาใหม่ <span className="req">*</span>
            </span>
            <input
              type="text"
              name="price"
              inputMode="decimal"
              defaultValue={typed}
              key={`price|${typed}`}
              autoComplete="off"
              className="input"
              aria-invalid={state?.error ? true : undefined}
              data-testid="ui-talad-013-ent-002-price"
            />
            {state?.error ? <span className="err">{state.error}</span> : null}
          </label>

          {state?.message ? (
            <div role="alert" className="alert">
              {state.message}
            </div>
          ) : null}

          <div className="modal-action">
            {/* state "loading" — save is off while saving */}
            <button type="submit" className="btn btn-primary" disabled={pending} data-testid="ui-talad-013-save">
              {pending ? <span className="loading loading-spinner loading-sm" aria-hidden="true" /> : null}
              บันทึก
            </button>
            <button type="button" className="btn" disabled={pending} onClick={() => dialog.current?.close()} data-testid="ui-talad-013-cancel">
              ยกเลิก
            </button>
          </div>
        </form>
      </dialog>
    </>
  );
}
