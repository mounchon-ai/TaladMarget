"use client";

import { useSearchParams } from "next/navigation";
import { DENIED_MESSAGE, DENIED_PARAM } from "@/lib/denied";

/** AC-talad-045 — shown on the sales page after a screen this role may not open sent the person back. */
export function DeniedNotice() {
  const params = useSearchParams();
  return params.get(DENIED_PARAM) === "1" ? (
    <div role="alert" className="toast">
      <div className="grow">{DENIED_MESSAGE}</div>
    </div>
  ) : null;
}
