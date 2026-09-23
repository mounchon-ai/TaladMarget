import { apiFetch } from "./api-client";

// Shapes the api answers (FE-talad-019) — API-019 · API-022.

/** One change of price (ENT-002). `source` is STOCK_SCREEN or SALES_SCREEN. */
export type PriceChange = {
  id: number;
  previousPrice: number;
  price: number;
  source: string;
  changedById: number;
  changedByName: string;
  changedAt: string;
};
export type PriceHistoryPage = { items: PriceChange[]; page: number; pageSize: number; total: number };

export type ProductDetail = {
  id: number;
  name: string;
  barcode: string | null;
  stockQty: number;
  lowStockThreshold: number;
  lowStock: boolean;
  price: number;
  hasImage: boolean;
  priceHistory: PriceHistoryPage;
};

/** API-019 · GET /api/products/{id}?historyPage= — null when unknown or no longer sold (PRODUCT_NOT_FOUND). */
export async function getProduct(id: number, historyPage = 1): Promise<ProductDetail | null> {
  const params = new URLSearchParams({ historyPage: String(historyPage) });
  const response = await apiFetch(`/api/products/${id}?${params}`);
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`GET /api/products/${id} answered ${response.status}`);
  return (await response.json()) as ProductDetail;
}

export type Repricing =
  | { ok: true; product: ProductDetail }
  | { ok: false; error: string }
  | { ok: false; refused: "gone" | "signedOut" | "forbidden" };

/**
 * API-022 · POST /api/products/{id}/prices — the price goes as typed (null when empty or not a number): the
 * rule is the domain's, and its sentence comes back under `price`. A product discontinued since the page
 * opened is PRODUCT_NOT_FOUND.
 */
export async function repriceProduct(id: number, price: number | null, source: "STOCK_SCREEN" | "SALES_SCREEN"): Promise<Repricing> {
  const response = await apiFetch(`/api/products/${id}/prices`, { method: "POST", body: JSON.stringify({ price, source }) });
  if (response.status === 201) return { ok: true, product: (await response.json()) as ProductDetail };
  if (response.status === 401) return { ok: false, refused: "signedOut" };
  if (response.status === 403) return { ok: false, refused: "forbidden" };
  const body = (await response.json().catch(() => null)) as { code?: string; errors?: { field: string; message: string }[] } | null;
  if (body?.code === "PRODUCT_NOT_FOUND") return { ok: false, refused: "gone" };
  const error = body?.code === "PRICE_INVALID" ? body.errors?.find((e) => e.field === "price")?.message : undefined;
  if (error) return { ok: false, error };
  throw new Error(`POST /api/products/${id}/prices answered ${response.status}`);
}
