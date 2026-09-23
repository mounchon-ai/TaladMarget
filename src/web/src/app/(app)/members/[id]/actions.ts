"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { DENIED_MESSAGE } from "@/lib/denied";
import { MEMBERS_PATH, MISSING_PARAM, validateMember, type EditFormState } from "@/lib/member-form";
import { editMember, hideMember, type MemberEdit, type MemberHiding } from "@/lib/members-api";

// UI-talad-006 action "save" → API-015. The form's own check runs first (BR-talad-002@v1 at ui); a phone
// another ACTIVE member holds (BR-talad-030@v1) comes back from the api under the phone field. On a refusal
// nothing in the system changes and the typed values stay in the form.
export async function saveMemberEdit(id: number, _prev: EditFormState, formData: FormData): Promise<EditFormState> {
  const values = { name: String(formData.get("name") ?? ""), phone: String(formData.get("phone") ?? "") };

  const errors = validateMember(values.name, values.phone);
  if (Object.keys(errors).length > 0) return { values, errors };

  let outcome: MemberEdit;
  try {
    outcome = await editMember(id, values.name, values.phone);
  } catch {
    return { values, errors: {}, message: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่" };
  }
  // redirect() throws to do its work — outside the try
  if ("signedOut" in outcome) redirect("/logout");
  if ("gone" in outcome) redirect(`${MEMBERS_PATH}?${MISSING_PARAM}=1`);
  if (!outcome.ok) {
    return Object.keys(outcome.errors).length > 0
      ? { values, errors: outcome.errors }
      : { values, errors: {}, message: "บันทึกไม่สำเร็จ กรุณาลองใหม่" };
  }
  revalidatePath(MEMBERS_PATH);
  revalidatePath(`${MEMBERS_PATH}/${id}`);
  return { values: { name: outcome.member.name, phone: outcome.member.phone }, errors: {}, saved: true };
}

// UI-talad-006 action "hide-member" → API-016, after the person confirmed (UC-talad-010). Hidden, the member
// is gone from search and the list, so the page goes back to UI-talad-004 (its declared destination).
export async function hideMemberAction(id: number): Promise<{ ok: false; message: string }> {
  let outcome: MemberHiding;
  try {
    outcome = await hideMember(id);
  } catch {
    return { ok: false, message: "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ กรุณาลองใหม่" };
  }
  // BR-talad-019@v1 — the api refused a seller; the sentence is the no-permission one (BR-talad-018@v1)
  if (outcome === "forbidden") return { ok: false, message: DENIED_MESSAGE };
  if (outcome === "signedOut") redirect("/logout");
  if (outcome === "gone") redirect(`${MEMBERS_PATH}?${MISSING_PARAM}=1`);
  revalidatePath(MEMBERS_PATH);
  redirect(MEMBERS_PATH);
}
