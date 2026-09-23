"use client";

import { useFormStatus } from "react-dom";

/** UI-talad-010 state "loading" — the search button is off until the results come back. */
export function StockSearchButton() {
  const { pending } = useFormStatus();
  return (
    <button type="submit" className="btn" disabled={pending} data-testid="ui-talad-010-search">
      ค้นหา
    </button>
  );
}
