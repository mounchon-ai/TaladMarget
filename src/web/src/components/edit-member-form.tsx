"use client";

import Link from "next/link";
import { useActionState } from "react";
import { formatBaht } from "@/lib/money";
import { MEMBERS_PATH, SAVED, type EditFormState } from "@/lib/member-form";
import type { Member } from "@/lib/members-api";

type Props = {
  member: Member;
  action: (prev: EditFormState, formData: FormData) => Promise<EditFormState>;
};

// UI-talad-006 zone form — arrangement as mockups/UI-talad-006/*.html draws it (reference); every
// data-testid is the wireframe's MCK-talad-007 control id. Both fields are always sent, so a name-only
// edit keeps the phone (API-015 replaces the whole record). The danger zone's ลบสมาชิก is FE-talad-016's.
export function EditMemberForm({ member, action }: Props) {
  const [state, formAction, pending] = useActionState(action, undefined);
  const errors = state?.errors ?? {};
  const values = state?.values ?? { name: member.name, phone: member.phone };

  return (
    <form action={formAction} noValidate className="card formcard">
      <h2>ข้อมูลสมาชิก</h2>

      <label className={errors.name ? "field invalid" : "field"}>
        <span className="lbl">
          ชื่อ <span className="req">*</span>
        </span>
        <input
          type="text"
          name="name"
          defaultValue={values.name}
          key={`name|${values.name}`}
          autoComplete="off"
          className="input"
          aria-invalid={errors.name ? true : undefined}
          data-testid="ui-talad-006-ent-004-name"
        />
        {errors.name ? <span className="err">{errors.name}</span> : null}
      </label>

      <label className={errors.phone ? "field invalid" : "field"}>
        <span className="lbl">
          เบอร์โทร <span className="req">*</span>
        </span>
        <input
          type="tel"
          name="phone"
          inputMode="numeric"
          defaultValue={values.phone}
          key={`phone|${values.phone}`}
          autoComplete="off"
          className="input"
          aria-invalid={errors.phone ? true : undefined}
          data-testid="ui-talad-006-ent-004-phone"
        />
        {errors.phone ? <span className="err">{errors.phone}</span> : null}
      </label>

      <div className="field">
        <span className="lbl">ยอดซื้อสะสม</span>
        <div className="value" data-testid="ui-talad-006-ent-004-accumulated-amount">
          {formatBaht(member.accumulatedAmount)}
        </div>
      </div>

      {state?.message ? (
        <div role="alert" className="alert">
          {state.message}
        </div>
      ) : null}
      {state?.saved ? (
        <div role="status" className="toast success">
          <div className="grow">{SAVED}</div>
        </div>
      ) : null}

      <div className="formactions">
        {/* state "loading" — save is off while saving */}
        <button type="submit" className="btn btn-primary" disabled={pending} data-testid="ui-talad-006-save">
          {pending ? (
            <>
              <span className="loading loading-spinner loading-sm" aria-hidden="true" />
              กำลังบันทึก…
            </>
          ) : (
            "บันทึก"
          )}
        </button>
        <Link href={MEMBERS_PATH} className="btn btn-ghost" data-testid="ui-talad-006-back">
          กลับ
        </Link>
      </div>
    </form>
  );
}
