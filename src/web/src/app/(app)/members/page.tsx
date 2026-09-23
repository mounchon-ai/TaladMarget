import { Suspense } from "react";
import type { Metadata } from "next";
import Form from "next/form";
import { HeaderRegister, MemberList, MemberListSkeleton } from "@/components/member-list";
import { MemberSearchButton } from "@/components/member-search-button";
import { requireScreen } from "@/lib/me";
import { MEMBERS_PATH } from "@/lib/member-form";
import { searchMembers } from "@/lib/members-api";

export const metadata: Metadata = { title: "สมาชิก · ตลาดมาร์เก็ต" };

type Search = Promise<{ q?: string; page?: string }>;

// UI-talad-004 สมาชิก (UC-talad-009 · FE-talad-012) — every member of the shop, whoever registered them
// (BR-talad-031@v1), found by the whole phone or part of the name (BR-talad-004@v1), 20 a page.
export default async function MembersPage({ searchParams }: { searchParams: Search }) {
  await requireScreen("UI-talad-004");
  const { q = "", page = "1" } = await searchParams;
  const pageNo = Math.max(1, Number.parseInt(page, 10) || 1);

  // one request, read by the header and the table; a failure is the table's to show (state "error")
  const loaded = searchMembers(q, pageNo).then(
    (p) => ({ ok: true as const, page: p }),
    () => ({ ok: false as const }),
  );

  return (
    <div data-screen="UI-talad-004">
      <div className="pagehead">
        <h1 className="grow">สมาชิก</h1>
        <Suspense key={`h|${q}|${pageNo}`} fallback={null}>
          <HeaderRegister loaded={loaded} />
        </Suspense>
      </div>
      <div className="card">
        {/* the term stays in the box whatever comes back (state "error": ข้อมูลที่กรอกค้างไว้ไม่หาย) */}
        <Form action={MEMBERS_PATH} className="inline" role="search">
          <input
            type="text"
            name="q"
            defaultValue={q}
            className="input"
            placeholder="เบอร์โทรเต็ม 10 หลัก หรือชื่อบางส่วน"
            aria-label="ค้นหา"
            data-testid="ui-talad-004-search-text"
          />
          <MemberSearchButton />
        </Form>
      </div>
      <Suspense key={`${q}|${pageNo}`} fallback={<MemberListSkeleton />}>
        <MemberList loaded={loaded} q={q} />
      </Suspense>
    </div>
  );
}
