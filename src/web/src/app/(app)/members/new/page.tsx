import type { Metadata } from "next";
import { RegisterMemberForm } from "@/components/register-member-form";
import { requireScreen } from "@/lib/me";
import { saveMember } from "./actions";

export const metadata: Metadata = { title: "สมัครสมาชิก · ตลาดมาร์เก็ต" };

// UI-talad-005 สมัครสมาชิก (UC-talad-007) — seller and owner alike (ACL-007 · BR-talad-021@v1).
export default async function RegisterMemberPage() {
  await requireScreen("UI-talad-005");

  return (
    <div data-screen="UI-talad-005">
      <div className="pagehead">
        <h1>สมัครสมาชิก</h1>
      </div>
      <RegisterMemberForm action={saveMember} />
    </div>
  );
}
