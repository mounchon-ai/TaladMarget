import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { PromotionForm } from "@/components/promotion-form";
import { requireScreen } from "@/lib/me";
import { MISSING_PARAM, PROMOTIONS_PATH, valuesOf } from "@/lib/promotion-form";
import { getPromotion } from "@/lib/promotions-api";
import { findProducts, savePromotionAction } from "../actions";

export const metadata: Metadata = { title: "แก้ไขโปรโมชั่น · ตลาดมาร์เก็ต" };

type Params = Promise<{ id: string }>;

// UI-talad-016 เพิ่ม/แก้ไขโปรโมชั่น, editing (UC-talad-021 alternate flow → API-028) — the conditions in force,
// saved as a new version: bills already paid keep the version they were paid under (AC-talad-062 ·
// BR-talad-035@v1). A promotion gone or discontinued goes back to the list, saying so.
export default async function EditPromotionPage({ params }: { params: Params }) {
  await requireScreen("UI-talad-016");
  const id = Number.parseInt((await params).id, 10);
  const promotion = Number.isSafeInteger(id) && id > 0 ? await getPromotion(id) : null;
  if (!promotion) redirect(`${PROMOTIONS_PATH}?${MISSING_PARAM}=1`);

  return (
    <div data-screen="UI-talad-016">
      <div className="pagehead">
        <div className="grow">
          <div className="crumbs">
            <Link href={PROMOTIONS_PATH}>โปรโมชั่น</Link> ›
          </div>
          <h1>แก้ไขโปรโมชั่น</h1>
        </div>
      </div>
      <PromotionForm initial={valuesOf(promotion)} action={savePromotionAction.bind(null, promotion.id)} find={findProducts} />
    </div>
  );
}
