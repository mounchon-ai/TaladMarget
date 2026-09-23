"use client";

import Link from "next/link";
import { useState, useTransition } from "react";
import { formatBaht } from "@/lib/money";
import { NO_MEMBER_FOUND } from "@/lib/member-form";
import type { Member } from "@/lib/members-api";
import type { CartMember } from "@/lib/sales-api";

type Result = { ok: true } | { ok: false; message: string };
type Search = { ok: true; members: Member[] } | { ok: false; message: string };

type Props = {
  member: CartMember | null;
  find: (q: string) => Promise<Search>;
  bind: (memberId: number) => Promise<Result>;
};

/**
 * UI-talad-002 zone member (UC-talad-009) — arrangement as mockups/UI-talad-002/*.html draws it (reference);
 * every data-testid is the wireframe's MCK-talad-002 control id. A search shows name, the whole phone and
 * what the member has bought (BR-talad-031@v1); เลือกสมาชิก binds that one to the cart. The member
 * discount it earns is shown by the totals zone (UC-talad-004), not here.
 * The term is a controlled input, so what was typed stays under the results.
 */
export function MemberPanel({ member, find, bind }: Props) {
  const [term, setTerm] = useState("");
  const [found, setFound] = useState<Member[] | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  function search(e: React.FormEvent) {
    e.preventDefault();
    setMessage(null);
    startTransition(async () => {
      const result = await find(term);
      if (result.ok) setFound(result.members);
      else setMessage(result.message);
    });
  }

  function choose(id: number) {
    setMessage(null);
    startTransition(async () => {
      const result = await bind(id);
      if (result.ok) setFound(null);
      else setMessage(result.message);
    });
  }

  return (
    <div className="card">
      <h2>สมาชิก</h2>

      {member ? (
        <div className="memberbox bound">
          <span className="avatar" aria-hidden="true">
            {member.name.slice(0, 1)}
          </span>
          <div className="grow">
            <div data-testid="ui-talad-002-ent-009-member">
              <strong>{member.name}</strong>
            </div>
            <span className="muted">{member.phone}</span>
          </div>
        </div>
      ) : null}

      <form className="inline" role="search" onSubmit={search}>
        <input
          type="text"
          value={term}
          onChange={(e) => setTerm(e.target.value)}
          className="input"
          placeholder="เบอร์โทร 10 หลัก หรือชื่อบางส่วน"
          aria-label="ค้นหาสมาชิก"
          data-testid="ui-talad-002-member-search"
        />
        <button type="submit" className="btn" disabled={pending} data-testid="ui-talad-002-search-member">
          ค้นหาสมาชิก
        </button>
      </form>

      {message ? (
        <div role="alert" className="toast">
          <div className="grow">{message}</div>
        </div>
      ) : null}

      {found && found.length === 0 ? <p className="muted">{NO_MEMBER_FOUND}</p> : null}
      {found?.map((m) => (
        <div key={m.id} className="memberbox" data-row-key={m.id}>
          <span className="avatar" aria-hidden="true">
            {m.name.slice(0, 1)}
          </span>
          <div className="grow">
            <strong className="clipline" title={m.name}>
              {m.name}
            </strong>
            <span className="muted">
              {m.phone} · ยอดซื้อสะสม {formatBaht(m.accumulatedAmount)}
            </span>
          </div>
          <button type="button" className="btn btn-sm" disabled={pending} onClick={() => choose(m.id)} data-testid="ui-talad-002-bind-member">
            เลือกสมาชิก
          </button>
        </div>
      ))}

      <div className="actions">
        <span className="grow muted">ไม่พบในระบบ?</span>
        <Link href="/members/new" className="btn btn-ghost btn-sm" data-testid="ui-talad-002-register-member">
          สมัครสมาชิกใหม่
        </Link>
      </div>
    </div>
  );
}
