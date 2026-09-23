import Link from "next/link";
import { DiscontinuePromotionButton } from "@/components/discontinue-promotion-button";
import { NO_PROMOTION, PROMOTIONS_PATH, statusLabel, typeLabel } from "@/lib/promotion-form";
import type { PromotionPage } from "@/lib/promotions-api";

type Loaded = { ok: true; page: PromotionPage } | { ok: false };
/** UI-talad-015 action discontinue (API-029) — the page hands the server action down; the list binds it per row. */
type Discontinue = (id: number) => Promise<{ ok: true } | { ok: false; message: string }>;

const listHref = (q: string, page: number) =>
  `${PROMOTIONS_PATH}?${new URLSearchParams({ ...(q ? { q } : {}), ...(page > 1 ? { page: String(page) } : {}) })}`;

// ENT-006 dates are calendar days (yyyy-MM-dd), read as such — no time zone can move them a day
const thaiDate = new Intl.DateTimeFormat("th-TH", { timeZone: "UTC", dateStyle: "medium" });
const showDate = (day: string) => thaiDate.format(new Date(`${day}T00:00:00Z`));

/** UI-talad-015 action add-promo → UI-talad-016. One per page: in the header, or in the empty state instead. */
export function AddPromotionLink() {
  return (
    <Link href={`${PROMOTIONS_PATH}/new`} className="btn btn-primary" data-testid="ui-talad-015-add-promo">
      สร้างโปรโมชั่น
    </Link>
  );
}

/** The header's create button — drawn only once there are rows, because the empty state carries its own. */
export async function HeaderAddPromotion({ loaded }: { loaded: Promise<Loaded> }) {
  const result = await loaded;
  return result.ok && result.page.items.length > 0 ? <AddPromotionLink /> : null;
}

/**
 * UI-talad-015 zone results (API-025) — the wireframe MCK-talad-015 is the only picture of this screen; every
 * data-testid is its control id. Each row carries data-row-key = the promotion's id (ENT-005 key) — not the
 * version's, which changes with every edit — because its controls repeat once per row (gate 126). แก้ไข then
 * ลบ sit in the first column as icons, in UIC-001's order (edit · delete) · UIC-002.
 */
export async function PromotionList({ loaded, q, discontinue }: { loaded: Promise<Loaded>; q: string; discontinue: Discontinue }) {
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
        <strong>{NO_PROMOTION}</strong>
        <div className="actions">
          <AddPromotionLink />
        </div>
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
            <th className="act">
              <span className="muted">แก้ไข · ลบ</span>
            </th>
            <th>ชื่อโปร</th>
            <th>รูปแบบ</th>
            <th>วันเริ่ม</th>
            <th>วันสิ้นสุด</th>
            <th>สถานะ</th>
          </tr>
        </thead>
        <tbody>
          {items.map((p) => (
            <tr key={p.id} data-row-key={p.id}>
              <td className="act">
                <Link href={`${PROMOTIONS_PATH}/${p.id}`} className="iconbtn" aria-label="แก้ไข" data-testid="ui-talad-015-edit-promo">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <path d="M12 20h9" />
                    <path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z" />
                  </svg>
                </Link>
                <DiscontinuePromotionButton name={p.name} discontinue={discontinue.bind(null, p.id)} />
              </td>
              <td className="clip" title={p.name} data-testid="ui-talad-015-ent-006-name">
                {p.name}
              </td>
              <td data-testid="ui-talad-015-ent-006-type">{typeLabel(p.type)}</td>
              <td data-testid="ui-talad-015-ent-006-start-date">{showDate(p.startDate)}</td>
              <td data-testid="ui-talad-015-ent-006-end-date">{p.endDate ? showDate(p.endDate) : <span className="muted">ไม่มีวันสิ้นสุด</span>}</td>
              <td data-testid="ui-talad-015-ent-005-status">{statusLabel(p.status)}</td>
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

/** UI-talad-015 state "loading" — the table's rows as outlines. */
export function PromotionListSkeleton() {
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
