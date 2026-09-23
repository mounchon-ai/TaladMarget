"use client";

import { useSearchParams } from "next/navigation";
import { REGISTERED, REGISTERED_PARAM } from "@/lib/member-form";

/** AC-talad-005 — UI-talad-005's save lands on the members page, and the frame says it worked, word for word. */
export function RegisteredNotice() {
  const params = useSearchParams();
  return params.get(REGISTERED_PARAM) === "1" ? (
    <div role="status" className="toast success">
      <div className="grow">{REGISTERED}</div>
    </div>
  ) : null;
}
