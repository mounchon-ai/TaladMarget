"use client";

import { useSearchParams } from "next/navigation";
import { MEMBER_MISSING, MISSING_PARAM, REGISTERED, REGISTERED_PARAM } from "@/lib/member-form";

/** AC-talad-005 — UI-talad-005's save lands on the members page, and the frame says it worked, word for word. */
export function RegisteredNotice() {
  const params = useSearchParams();
  return params.get(REGISTERED_PARAM) === "1" ? (
    <div role="status" className="toast success">
      <div className="grow">{REGISTERED}</div>
    </div>
  ) : null;
}

/** UI-talad-006 state "error" — the member was hidden while their page was open; the list says so. */
export function MemberMissingNotice() {
  const params = useSearchParams();
  return params.get(MISSING_PARAM) === "1" ? (
    <div role="alert" className="toast">
      <div className="grow">{MEMBER_MISSING}</div>
    </div>
  ) : null;
}
