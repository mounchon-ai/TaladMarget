import { Suspense } from "react";
import type { Metadata } from "next";
import Form from "next/form";
import { HeaderAddPromotion, PromotionList, PromotionListSkeleton } from "@/components/promotion-list";
import { PromotionSearchButton } from "@/components/promotion-search-button";
import { requireScreen } from "@/lib/me";
import { MISSING_PARAM, NO_PROMOTION, PROMOTIONS_PATH, SAVED, SAVED_PARAM } from "@/lib/promotion-form";
import { searchPromotions } from "@/lib/promotions-api";

export const metadata: Metadata = { title: "โปรโมชั่น · ตลาดมาร์เก็ต" };

type Search = Promise<{ q?: string; page?: string; saved?: string; gone?: string }>;

// UI-talad-015 โปรโมชั่น (UC-talad-021 · FE-talad-026) — the owner's screen (ACL-023): the promotions still in
// use with the conditions in force, found by part of the name, 20 a page (NFR-talad-006).
export default async function PromotionsPage({ searchParams }: { searchParams: Search }) {
  await requireScreen("UI-talad-015");
  const params = await searchParams;
  const q = params.q ?? "";
  const pageNo = Math.max(1, Number.parseInt(params.page ?? "1", 10) || 1);

  // one request, read by the header and the table; a failure is the table's to show (state "error")
  const loaded = searchPromotions(q, pageNo).then(
    (p) => ({ ok: true as const, page: p }),
    () => ({ ok: false as const }),
  );

  return (
    <div data-screen="UI-talad-015">
      <div className="pagehead">
        <h1 className="grow">โปรโมชั่น</h1>
        <Suspense key={`h|${q}|${pageNo}`} fallback={null}>
          <HeaderAddPromotion loaded={loaded} />
        </Suspense>
      </div>
      {params[SAVED_PARAM] === "1" ? (
        <div role="status" className="toast success">
          <div className="grow">{SAVED}</div>
        </div>
      ) : null}
      {params[MISSING_PARAM] === "1" ? (
        <div role="alert" className="toast">
          <div className="grow">{NO_PROMOTION}</div>
        </div>
      ) : null}
      <div className="card">
        {/* the term stays in the box whatever comes back (state "error": ข้อมูลที่กรอกค้างไว้ไม่หาย) */}
        <Form action={PROMOTIONS_PATH} className="inline" role="search">
          <input
            type="text"
            name="q"
            defaultValue={q}
            className="input"
            placeholder="ชื่อโปรบางส่วน"
            aria-label="ค้นหา"
            data-testid="ui-talad-015-search-text"
          />
          <PromotionSearchButton />
        </Form>
      </div>
      <Suspense key={`${q}|${pageNo}`} fallback={<PromotionListSkeleton />}>
        <PromotionList loaded={loaded} q={q} />
      </Suspense>
    </div>
  );
}
