"use client";

import { useFormStatus } from "react-dom";

/** UI-talad-004 state "loading" — the search button is off until the results come back. */
export function MemberSearchButton() {
  const { pending } = useFormStatus();
  return (
    <button type="submit" className="btn" disabled={pending} data-testid="ui-talad-004-search">
      ค้นหา
    </button>
  );
}
