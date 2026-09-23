import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { EditMemberForm } from "@/components/edit-member-form";
import { HideMemberZone } from "@/components/hide-member-zone";
import { requireScreen } from "@/lib/me";
import { MEMBERS_PATH, MISSING_PARAM } from "@/lib/member-form";
import { getMember } from "@/lib/members-api";
import { hideMemberAction, saveMemberEdit } from "./actions";

export const metadata: Metadata = { title: "ข้อมูลสมาชิก · ตลาดมาร์เก็ต" };

type Params = Promise<{ id: string }>;

// UI-talad-006 ข้อมูลสมาชิก (UC-talad-008 · UC-talad-010) — seller and owner alike edit any ACTIVE member
// (ACL-008); only the owner sees ลบสมาชิก (BR-talad-019@v1 · ACL-014), and the api refuses anyone else anyway.
// A member who is gone or hidden sends the person back to the list with "ไม่พบสมาชิก" (state "error").
// Opened as its own page for now; UIC-003's modal over the list is still open (see FE-talad-014 handoff).
export default async function MemberPage({ params }: { params: Params }) {
  const me = await requireScreen("UI-talad-006");
  const id = Number.parseInt((await params).id, 10);
  const member = Number.isSafeInteger(id) && id > 0 ? await getMember(id) : null;
  if (!member) redirect(`${MEMBERS_PATH}?${MISSING_PARAM}=1`);

  return (
    <div data-screen="UI-talad-006">
      <div className="pagehead">
        <div className="grow">
          <div className="crumbs">
            <Link href={MEMBERS_PATH}>สมาชิก</Link> ›
          </div>
          <h1>ข้อมูลสมาชิก</h1>
        </div>
      </div>
      <EditMemberForm member={member} action={saveMemberEdit.bind(null, member.id)} />
      {me.role === "Owner" ? <HideMemberZone name={member.name} hide={hideMemberAction.bind(null, member.id)} /> : null}
    </div>
  );
}
