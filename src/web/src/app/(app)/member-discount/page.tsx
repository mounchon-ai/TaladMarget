import type { Metadata } from "next";
import { MemberDiscountForm } from "@/components/member-discount-form";
import { requireScreen } from "@/lib/me";
import { getMemberDiscount } from "@/lib/settings-api";
import { saveMemberDiscount } from "./actions";

export const metadata: Metadata = { title: "ส่วนลดสมาชิก · ตลาดมาร์เก็ต" };

// UI-talad-017 ตั้งส่วนลดสมาชิก (UC-talad-023) — the owner's screen: a seller opening it directly is sent
// to the sales page with the no-permission notice before anything here is read (state "unauthorized").
export default async function MemberDiscountPage() {
  await requireScreen("UI-talad-017");
  const current = await getMemberDiscount();

  return (
    <div data-screen="UI-talad-017">
      <div className="pagehead">
        <h1>ตั้งส่วนลดสมาชิก</h1>
      </div>
      <MemberDiscountForm initial={current} action={saveMemberDiscount.bind(null, current)} />
    </div>
  );
}
