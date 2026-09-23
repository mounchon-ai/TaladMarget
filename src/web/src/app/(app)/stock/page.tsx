import { Suspense } from "react";
import type { Metadata } from "next";
import Form from "next/form";
import { STOCK_PATH, StockList, StockListSkeleton } from "@/components/stock-list";
import { StockSearchButton } from "@/components/stock-search-button";
import { requireScreen } from "@/lib/me";
import { searchProducts } from "@/lib/sales-api";

export const metadata: Metadata = { title: "สต็อก · ตลาดมาร์เก็ต" };

type Search = Promise<{ q?: string; page?: string }>;

// UI-talad-010 สต็อก (UC-talad-019 · FE-talad-018) — the owner's screen (ACL-021): products still sold with what
// is left and the low-stock badge at each product's own threshold, found by part of the name or the whole
// barcode, 20 a page (NFR-talad-006).
export default async function StockPage({ searchParams }: { searchParams: Search }) {
  await requireScreen("UI-talad-010");
  const { q = "", page = "1" } = await searchParams;
  const pageNo = Math.max(1, Number.parseInt(page, 10) || 1);

  // a failure is the table's to show (state "error")
  const loaded = searchProducts(q, pageNo).then(
    (p) => ({ ok: true as const, page: p }),
    () => ({ ok: false as const }),
  );

  return (
    <div data-screen="UI-talad-010">
      <div className="pagehead">
        <h1 className="grow">สต็อก</h1>
      </div>
      <div className="card">
        {/* the term stays in the box whatever comes back (state "error": ข้อมูลที่กรอกค้างไว้ไม่หาย) */}
        <Form action={STOCK_PATH} className="inline" role="search">
          <input
            type="text"
            name="q"
            defaultValue={q}
            className="input"
            placeholder="ชื่อสินค้าบางส่วนหรือเลขบาร์โค้ด"
            aria-label="ค้นหา"
            data-testid="ui-talad-010-search-text"
          />
          <StockSearchButton />
        </Form>
      </div>
      <Suspense key={`${q}|${pageNo}`} fallback={<StockListSkeleton />}>
        <StockList loaded={loaded} q={q} />
      </Suspense>
    </div>
  );
}
