"use client";

import { useFormStatus } from "react-dom";

/** UI-talad-015 state "loading" — the search button is off until the results come back. */
export function PromotionSearchButton() {
  const { pending } = useFormStatus();
  return (
    <button type="submit" className="btn" disabled={pending} data-testid="ui-talad-015-search">
      ค้นหา
    </button>
  );
}
