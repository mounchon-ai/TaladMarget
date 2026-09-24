import Link from "next/link";
import { NO_ROWS, thaiTime } from "@/components/price-history";
import { productDetailHref } from "@/lib/product-detail-href";
import type { StockAdjustmentPage } from "@/lib/products-api";
import { formatDelta, reasonLabel } from "@/lib/stock-adjustment-form";

/**
 * UI-talad-012 zone adjustments (API-019 · FE-talad-024) — the columns the wireframe MCK-talad-013 draws: จำนวนที่ปรับ ·
 * เหตุผล · ผู้ปรับ · เวลาที่ปรับ (Thai time) · หมายเหตุ, newest first, 20 a page on its own parameter (state
 * "overflow"); paging it keeps the price history on its page. Each row carries data-row-key = the adjustment's id
 * (ENT-003 key) because its fields repeat once per row (gate 126).
 */
export function StockAdjustments({ adjustments, productId, historyPage = 1 }: { adjustments: StockAdjustmentPage; productId: number; historyPage?: number }) {
  const { items, page, pageSize, total } = adjustments;
  if (items.length === 0) return <p className="muted">{NO_ROWS}</p>;

  const pages = Math.max(1, Math.ceil(total / pageSize));
  const href = (n: number) => productDetailHref(productId, historyPage, n);
  return (
    <div className="tablewrap">
      <table>
        <thead>
          <tr>
            <th className="num">จำนวนที่ปรับ</th>
            <th>เหตุผล</th>
            <th>ผู้ปรับ</th>
            <th>เวลาที่ปรับ</th>
            <th>หมายเหตุ</th>
          </tr>
        </thead>
        <tbody>
          {items.map((a) => (
            <tr key={a.id} data-row-key={a.id}>
              <td className="num" data-testid="ui-talad-012-ent-003-quantity-delta">
                {formatDelta(a.quantityDelta)}
              </td>
              <td data-testid="ui-talad-012-ent-003-reason">{reasonLabel(a.reason)}</td>
              <td data-testid="ui-talad-012-ent-003-adjusted-by">{a.adjustedByName}</td>
              <td data-testid="ui-talad-012-ent-003-adjusted-at">{thaiTime.format(new Date(a.adjustedAt))}</td>
              <td data-testid="ui-talad-012-ent-003-note">{a.note ?? ""}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {pages > 1 ? (
        <div className="tablefoot">
          <span className="grow">
            หน้า {page} / {pages}
          </span>
          {page > 1 ? <Link href={href(page - 1)}>‹ ก่อนหน้า</Link> : null}
          {page < pages ? <Link href={href(page + 1)}>ถัดไป ›</Link> : null}
        </div>
      ) : null}
    </div>
  );
}
