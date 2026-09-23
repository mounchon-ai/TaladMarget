// UI-talad-005 สมัครสมาชิก — BR-talad-002@v1 is checked here before the api is asked (interfaces.json
// ruleEnforcement: ui · api · domain). Pure, so the server action and a test ask it the same way.

export type MemberField = "name" | "phone";
export type MemberFieldErrors = Partial<Record<MemberField, string>>;

/** The sentences UI-talad-005 declares, word for word — the same ones the api answers. */
export const NAME_REQUIRED = "กรุณากรอกชื่อ";
export const PHONE_FORMAT = "เบอร์โทรต้องเป็นตัวเลข 10 หลัก ขึ้นต้นด้วย 0";
export const REGISTERED = "สมัครสมาชิกสำเร็จ";

/** Save and cancel both go to UI-talad-004 หน้าสมาชิก; a save carries this so the frame says it worked. */
export const MEMBERS_PATH = "/members";
export const REGISTERED_PARAM = "registered";

/** ENT-004.phone — dashes and spaces come out before checking and before storing. */
export const normalizePhone = (phone: string) => phone.replace(/[-\s]/g, "");

// ASCII 0-9 only: \d in a /u regex, and the api's char.IsDigit, would also take Thai digits (๐-๙)
const PHONE = /^0[0-9]{9}$/;

/** Every field that is wrong, not just the first. */
export function validateMember(name: string, phone: string): MemberFieldErrors {
  const errors: MemberFieldErrors = {};
  if (name.trim() === "") errors.name = NAME_REQUIRED;
  if (!PHONE.test(normalizePhone(phone))) errors.phone = PHONE_FORMAT;
  return errors;
}

/** What the form shows after a save that did not go through — the typed values come back so nothing is lost. */
export type RegisterFormState =
  | { values: { name: string; phone: string }; errors: MemberFieldErrors; message?: string }
  | undefined;
