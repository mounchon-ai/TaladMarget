import Link from "next/link";
import { formatBaht } from "@/lib/money";
import type { ProductPage } from "@/lib/sales-api";

type Loaded = { ok: true; page: ProductPage } | { ok: false };

export const STOCK_PATH = "/stock";
/** UI-talad-010 state "empty" — word for word. */
export const NO_PRODUCT = "ไม่พบสินค้า";
/** BR-talad-008@v1 — the badge, the same word the sales cards carry. */
export const LOW_STOCK = "ใกล้หมด";

const listHref = (q: string, page: number) =>
  `${STOCK_PATH}?${new URLSearchParams({ ...(q ? { q } : {}), ...(page > 1 ? { page: String(page) } : {}) })}`;

/**
 * UI-talad-010 zone results (API-003, the same search the sales screen reads) — the wireframe MCK-talad-010 is
 * the only picture of this screen; every data-testid is its control id. Each row carries data-row-key = the
 * product's id (ENT-001 key) because its fields repeat once per row (gate 126). The badge is the api's flag —
 * the page never compares stock with the threshold itself (BR-talad-008@v1 is the domain's).
 * Not drawn yet: เพิ่มสินค้า (UI-talad-011 has no unit — GAP-talad-002), ดูสินค้า (UI-talad-012 is
 * FE-talad-020's), Export Excel (FE-talad-046's).
 */
export async function StockList({ loaded, q }: { loaded: Promise<Loaded>; q: string }) {
  const result = await loaded;

  if (!result.ok) {
    return (
      <div role="alert" className="toast">
        <div className="grow">
          <strong>โหลดข้อมูลไม่สำเร็จ</strong>
        </div>
        <Link href={listHref(q, 1)} className="btn btn-sm">
          ลองใหม่
        </Link>
      </div>
    );
  }

  const { items, page, pageSize, total } = result.page;
  if (items.length === 0) {
    return (
      <div className="empty">
        <strong>{NO_PRODUCT}</strong>
      </div>
    );
  }

  const pages = Math.max(1, Math.ceil(total / pageSize));
  const from = (page - 1) * pageSize + 1;
  return (
    <div className="tablewrap">
      <table>
        <thead>
          <tr>
            <th>ชื่อสินค้า</th>
            <th>บาร์โค้ด</th>
            <th className="num">ราคา</th>
            <th className="num">คงเหลือ</th>
            <th>ป้ายใกล้หมด</th>
          </tr>
        </thead>
        <tbody>
          {items.map((p) => (
            <tr key={p.id} data-row-key={p.id}>
              <td className="clip" title={p.name} data-testid="ui-talad-010-ent-001-name">
                {p.name}
              </td>
              <td data-testid="ui-talad-010-ent-001-barcode">{p.barcode ?? <span className="muted">—</span>}</td>
              <td className="num" data-testid="ui-talad-010-ent-002-price">
                {formatBaht(p.price)}
              </td>
              <td className="num" data-testid="ui-talad-010-ent-001-stock-qty">
                {p.stockQty.toLocaleString("th-TH")}
              </td>
              <td data-testid="ui-talad-010-ent-001-low-stock-threshold">{p.lowStock ? <span className="tag low">{LOW_STOCK}</span> : null}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="tablefoot">
        <span className="grow">
          แสดง {from}–{from + items.length - 1} จาก {total} แถว
        </span>
        {page > 1 ? <Link href={listHref(q, page - 1)}>‹ ก่อนหน้า</Link> : null}
        {page < pages ? <Link href={listHref(q, page + 1)}>ถัดไป ›</Link> : null}
      </div>
    </div>
  );
}

/** UI-talad-010 state "loading" — the table's rows as outlines. */
export function StockListSkeleton() {
  return (
    <div className="tablewrap" aria-busy="true">
      <table>
        <tbody>
          {[0, 1, 2].map((i) => (
            <tr key={i}>
              <td>
                <span className="skeleton" />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
