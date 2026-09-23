"use client";

import { useActionState } from "react";
import type { DiscountFormState } from "@/app/(app)/member-discount/actions";
import type { MemberDiscount } from "@/lib/settings-api";

type Props = {
  initial: MemberDiscount;
  action: (prev: DiscountFormState, formData: FormData) => Promise<DiscountFormState>;
};

/** UI-talad-017 state "empty" — word for word. */
export const NO_MEMBER_DISCOUNT = "ยังไม่มีส่วนลดสมาชิก";

const thaiTime = new Intl.DateTimeFormat("th-TH", { timeZone: "Asia/Bangkok", dateStyle: "medium", timeStyle: "short" });

/**
 * UI-talad-017 zone setting — the wireframe MCK is the only picture of this screen; every data-testid is
 * its control id. Who set the % and when are shown as the api returns them (ENT-007.changedBy ·
 * changedAt, Thai time). noValidate: the rule's own sentence, not the browser's bubble, answers a bad %.
 */
export function MemberDiscountForm({ initial, action }: Props) {
  const [state, formAction, pending] = useActionState(action, undefined);
  const current = state?.current ?? initial;
  const typed = state?.typed ?? String(current.ratePercent);
  const neverSet = current.changedAt === null;

  return (
    <form action={formAction} noValidate className="card formcard">
      {neverSet ? <p className="muted">{NO_MEMBER_DISCOUNT}</p> : null}

      <label className={state?.error ? "field invalid" : "field"}>
        <span className="lbl">
          % ส่วนลดสมาชิก <span className="req">*</span>
        </span>
        <input
          type="text"
          name="ratePercent"
          inputMode="numeric"
          defaultValue={typed}
          key={typed}
          autoComplete="off"
          className="input"
          aria-invalid={state?.error ? true : undefined}
          data-testid="ui-talad-017-ent-007-rate-percent"
        />
        {state?.error ? <span className="err">{state.error}</span> : null}
      </label>

      <div className="field">
        <span className="lbl">ตั้งล่าสุดโดย</span>
        <div data-testid="ui-talad-017-ent-007-changed-by">{current.changedByName ?? "—"}</div>
      </div>
      <div className="field">
        <span className="lbl">ตั้งล่าสุดเมื่อ</span>
        <div data-testid="ui-talad-017-ent-007-changed-at">{current.changedAt ? thaiTime.format(new Date(current.changedAt)) : "—"}</div>
      </div>

      {state?.message ? (
        <div role="alert" className="alert">
          {state.message}
        </div>
      ) : null}

      <div className="formactions">
        {/* state "loading" — save is off while saving */}
        <button type="submit" className="btn btn-primary" disabled={pending} data-testid="ui-talad-017-save">
          {pending ? (
            <>
              <span className="loading loading-spinner loading-sm" aria-hidden="true" />
              กำลังบันทึก…
            </>
          ) : (
            "บันทึก"
          )}
        </button>
      </div>
    </form>
  );
}
