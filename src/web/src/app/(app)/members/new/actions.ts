"use server";

import { redirect } from "next/navigation";
import { MEMBERS_PATH, REGISTERED_PARAM, validateMember, type RegisterFormState } from "@/lib/member-form";
import { registerMember, type Registration } from "@/lib/members-api";

// UI-talad-005 action "save" → API-013. The form's own check runs first, so a wrong field never reaches
// the api (BR-talad-002@v1 at ui); what only the api knows — a phone an ACTIVE member holds
// (BR-talad-030@v1) — comes back under the phone field. Only messages cross to the client.
export async function saveMember(_prev: RegisterFormState, formData: FormData): Promise<RegisterFormState> {
  const values = { name: String(formData.get("name") ?? ""), phone: String(formData.get("phone") ?? "") };

  const errors = validateMember(values.name, values.phone);
  if (Object.keys(errors).length > 0) return { values, errors };

  let outcome: Registration;
  try {
    outcome = await registerMember(values.name, values.phone);
  } catch {
    return { values, errors: {}, message: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่" };
  }
  // redirect() throws to do its work — kept outside the try so a saved member is never reported as a failure
  if ("signedOut" in outcome) redirect("/logout");
  if (!outcome.ok) {
    return Object.keys(outcome.errors).length > 0
      ? { values, errors: outcome.errors }
      : { values, errors: {}, message: "บันทึกไม่สำเร็จ กรุณาลองใหม่" };
  }
  redirect(`${MEMBERS_PATH}?${REGISTERED_PARAM}=1`);
}
