import Link from "next/link";
import { formatBaht } from "@/lib/money";
import type { PriceHistoryPage } from "@/lib/products-api";

/** UI-talad-012 state "empty" in a section — word for word. */
export const NO_ROWS = "ยังไม่มีรายการ";

const thaiTime = new Intl.DateTimeFormat("th-TH", { timeZone: "Asia/Bangkok", dateStyle: "medium", timeStyle: "short" });

/**
 * UI-talad-012 zone price-history (API-019) — the columns the wireframe MCK-talad-012 draws: ราคาเดิม · ผู้แก้ราคา ·
 * เวลาที่แก้ราคา (Thai time), newest first, 20 a page on its own parameter (state "overflow"). Each row carries
 * data-row-key = the price version's id (ENT-002 key) because its fields repeat once per row (gate 126).
 */
export function PriceHistory({ history, productId }: { history: PriceHistoryPage; productId: number }) {
  const { items, page, pageSize, total } = history;
  if (items.length === 0) return <p className="muted">{NO_ROWS}</p>;

  const pages = Math.max(1, Math.ceil(total / pageSize));
  const href = (n: number) => `/stock/${productId}${n > 1 ? `?historyPage=${n}` : ""}`;
  return (
    <div className="tablewrap">
      <table>
        <thead>
          <tr>
            <th className="num">ราคาเดิม</th>
            <th>ผู้แก้ราคา</th>
            <th>เวลาที่แก้ราคา</th>
          </tr>
        </thead>
        <tbody>
          {items.map((h) => (
            <tr key={h.id} data-row-key={h.id}>
              <td className="num" data-testid="ui-talad-012-ent-002-previous-price">
                {formatBaht(h.previousPrice)}
              </td>
              <td data-testid="ui-talad-012-ent-002-changed-by">{h.changedByName}</td>
              <td data-testid="ui-talad-012-ent-002-changed-at">{thaiTime.format(new Date(h.changedAt))}</td>
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
