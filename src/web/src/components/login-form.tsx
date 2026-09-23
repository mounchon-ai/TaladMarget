"use client";

import { useActionState } from "react";
import type { LoginFormState } from "@/lib/login-errors";

type Props = {
  action: (prev: LoginFormState, formData: FormData) => Promise<LoginFormState>;
  next: string;
};

// UI-talad-001 เข้าสู่ระบบ — arrangement follows mockups/UI-talad-001/*.html (reference);
// every data-testid is the wireframe's MCK-talad-001 control id, copied as-is (normative).
export function LoginForm({ action, next }: Props) {
  const [state, formAction, pending] = useActionState(action, undefined);

  return (
    <form action={formAction} className="authcard" noValidate={false}>
      <span className="wordmark">
        <span className="mark">ต</span>ตลาดมาร์เก็ต
      </span>
      <h1>เข้าสู่ระบบ</h1>
      <p className="sub">ใช้ชื่อผู้ใช้และรหัสผ่านที่เจ้าของร้านตั้งให้</p>

      <input type="hidden" name="next" value={next} />

      <label className={state?.field === "username" ? "field invalid" : "field"}>
        <span className="lbl">
          ชื่อผู้ใช้ <span className="req">*</span>
        </span>
        <input
          type="text"
          name="username"
          required
          autoComplete="username"
          placeholder="ชื่อผู้ใช้"
          className="input"
          data-testid="ui-talad-001-ent-008-username"
        />
        {state?.field === "username" ? <span className="err">{state.message}</span> : null}
      </label>

      <label className={state?.field === "password" ? "field invalid" : "field"}>
        <span className="lbl">
          รหัสผ่าน <span className="req">*</span>
        </span>
        <input
          type="password"
          name="password"
          required
          autoComplete="current-password"
          placeholder="รหัสผ่าน"
          className="input"
          data-testid="ui-talad-001-password"
        />
        {state?.field === "password" ? <span className="err">{state.message}</span> : null}
      </label>

      <button type="submit" className="btn btn-primary btn-block" disabled={pending} data-testid="ui-talad-001-sign-in">
        {pending ? (
          <>
            <span className="loading loading-spinner loading-sm" aria-hidden="true" />
            กำลังตรวจสอบ…
          </>
        ) : (
          "เข้าสู่ระบบ"
        )}
      </button>

      {state && state.field === null ? (
        <div role="alert" className="alert">
          {state.message}
        </div>
      ) : null}
    </form>
  );
}
