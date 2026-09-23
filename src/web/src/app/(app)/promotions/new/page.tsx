import type { Metadata } from "next";
import Link from "next/link";
import { PromotionForm } from "@/components/promotion-form";
import { requireScreen } from "@/lib/me";
import { EMPTY_VALUES, PROMOTIONS_PATH } from "@/lib/promotion-form";
import { findProducts, savePromotionAction } from "../actions";

export const metadata: Metadata = { title: "สร้างโปรโมชั่น · ตลาดมาร์เก็ต" };

// UI-talad-016 เพิ่ม/แก้ไขโปรโมชั่น, creating (UC-talad-021 main flow → API-027) — state "empty" does not happen:
// the empty form is how a new promotion starts.
export default async function NewPromotionPage() {
  await requireScreen("UI-talad-016");

  return (
    <div data-screen="UI-talad-016">
      <div className="pagehead">
        <div className="grow">
          <div className="crumbs">
            <Link href={PROMOTIONS_PATH}>โปรโมชั่น</Link> ›
          </div>
          <h1>สร้างโปรโมชั่น</h1>
        </div>
      </div>
      <PromotionForm initial={EMPTY_VALUES} action={savePromotionAction.bind(null, null)} find={findProducts} />
    </div>
  );
}
