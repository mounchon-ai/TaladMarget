/**
 * UI-talad-012 — the product page with each section on its own page (state "overflow"); paging one section keeps
 * the other where it was. Page 1 is left out of the URL.
 */
export function productDetailHref(productId: number, historyPage: number, adjustmentsPage: number): string {
  const params = new URLSearchParams();
  if (historyPage > 1) params.set("historyPage", String(historyPage));
  if (adjustmentsPage > 1) params.set("adjustmentsPage", String(adjustmentsPage));
  const query = params.toString();
  return `/stock/${productId}${query ? `?${query}` : ""}`;
}
