import Link from "next/link";
import { formatBaht } from "@/lib/money";
import { MEMBERS_PATH, NO_MEMBER_FOUND } from "@/lib/member-form";
import type { MemberPage } from "@/lib/members-api";

type Loaded = { ok: true; page: MemberPage } | { ok: false };

const listHref = (q: string, page: number) =>
  `${MEMBERS_PATH}?${new URLSearchParams({ ...(q ? { q } : {}), ...(page > 1 ? { page: String(page) } : {}) })}`;

/** UI-talad-004 action register → UI-talad-005. One per page: in the header, or in the empty state instead. */
export function RegisterLink() {
  return (
    <Link href={`${MEMBERS_PATH}/new`} className="btn btn-primary" data-testid="ui-talad-004-register">
      สมัครสมาชิก
    </Link>
  );
}

/** The header's register button — drawn only once there are rows, because the empty state carries its own. */
export async function HeaderRegister({ loaded }: { loaded: Promise<Loaded> }) {
  const result = await loaded;
  return result.ok && result.page.items.length > 0 ? <RegisterLink /> : null;
}

/**
 * UI-talad-004 zone results (API-012) — arrangement as mockups/UI-talad-004/*.html draws it (reference);
 * every data-testid is the wireframe's MCK-talad-005 control id. Each row carries data-row-key = the
 * member's id (ENT-004 key) because its controls repeat once per row (gate 126). The view action sits
 * in the first column as an icon (UIC-001 · UIC-002) and links to UI-talad-006.
 */
export async function MemberList({ loaded, q }: { loaded: Promise<Loaded>; q: string }) {
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
        <strong>{NO_MEMBER_FOUND}</strong>
        <div className="actions">
          <RegisterLink />
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
              <span className="muted">ดู</span>
            </th>
            <th>ชื่อ</th>
            <th>เบอร์โทร</th>
            <th className="num">ยอดซื้อสะสม</th>
          </tr>
        </thead>
        <tbody>
          {items.map((m) => (
            <tr key={m.id} data-row-key={m.id}>
              <td className="act">
                <Link href={`${MEMBERS_PATH}/${m.id}`} className="iconbtn" aria-label="ดูข้อมูล" data-testid="ui-talad-004-open-member">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                    <circle cx="12" cy="12" r="3" />
                  </svg>
                </Link>
              </td>
              <td className="clip" title={m.name} data-testid="ui-talad-004-ent-004-name">
                {m.name}
              </td>
              <td data-testid="ui-talad-004-ent-004-phone">{m.phone}</td>
              <td className="num" data-testid="ui-talad-004-ent-004-accumulated-amount">
                {formatBaht(m.accumulatedAmount)}
              </td>
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

/** UI-talad-004 state "loading" — the table's rows as outlines. */
export function MemberListSkeleton() {
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
