"use client";

import Link from "next/link";
import { useActionState } from "react";
import { MEMBERS_PATH, type RegisterFormState } from "@/lib/member-form";

type Props = { action: (prev: RegisterFormState, formData: FormData) => Promise<RegisterFormState> };

// UI-talad-005 สมัครสมาชิก — the wireframe MCK-talad-006 (normative) is the only picture of this screen;
// every data-testid is its control id, copied as-is. noValidate: the browser's own "required" bubble
// would stand in for the declared "กรุณากรอกชื่อ" under the field.
export function RegisterMemberForm({ action }: Props) {
  const [state, formAction, pending] = useActionState(action, undefined);
  const errors = state?.errors ?? {};

  return (
    <form action={formAction} noValidate className="card formcard">
      <label className={errors.name ? "field invalid" : "field"}>
        <span className="lbl">
          ชื่อ <span className="req">*</span>
        </span>
        <input
          type="text"
          name="name"
          defaultValue={state?.values.name}
          autoComplete="off"
          className="input"
          aria-invalid={errors.name ? true : undefined}
          data-testid="ui-talad-005-ent-004-name"
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
          defaultValue={state?.values.phone}
          autoComplete="off"
          placeholder="0812345678"
          className="input"
          aria-invalid={errors.phone ? true : undefined}
          data-testid="ui-talad-005-ent-004-phone"
        />
        {errors.phone ? <span className="err">{errors.phone}</span> : null}
      </label>

      {state?.message ? (
        <div role="alert" className="alert">
          {state.message}
        </div>
      ) : null}

      <div className="formactions">
        {/* state "loading" — save is off while saving, so a second press cannot make a second member */}
        <button type="submit" className="btn btn-primary" disabled={pending} data-testid="ui-talad-005-save">
          {pending ? (
            <>
              <span className="loading loading-spinner loading-sm" aria-hidden="true" />
              กำลังบันทึก…
            </>
          ) : (
            "บันทึก"
          )}
        </button>
        <Link href={MEMBERS_PATH} className="btn btn-ghost" data-testid="ui-talad-005-cancel">
          ยกเลิก
        </Link>
      </div>
    </form>
  );
}
