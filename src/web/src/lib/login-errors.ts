import type { LoginFailed } from "./auth-api";

export type LoginFormState = {
  /** which field the message sits under — the mockup's error state draws it under the field it is about */
  field: "username" | "password" | null;
  message: string;
} | undefined;

/** The mockup's own heading for a failed sign-in (mockups/UI-talad-001/error.html). */
export const SIGN_IN_FAILED = "เข้าสู่ระบบไม่สำเร็จ";

/**
 * BR-talad-005@v1 — the two failures the api tells apart are shown word for word as the api sends
 * them. A disabled account's wording is still open at req, so it gets the mockup heading and nothing
 * invented; a server nobody could reach asks the person to try again (UI-talad-001 state "error").
 */
export function toFormState(failure: LoginFailed): NonNullable<LoginFormState> {
  switch (failure.code) {
    case "USER_NOT_FOUND":
      return { field: "username", message: failure.message ?? SIGN_IN_FAILED };
    case "WRONG_PASSWORD":
      return { field: "password", message: failure.message ?? SIGN_IN_FAILED };
    case "NETWORK":
      return { field: null, message: `${SIGN_IN_FAILED} — เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่` };
    default:
      return { field: null, message: failure.message ?? SIGN_IN_FAILED };
  }
}
