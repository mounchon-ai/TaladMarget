import { apiFetch } from "./api-client";

// Shapes the api answers (FE-talad-019 · 023) — API-019 · API-022 · API-024.

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

/** ENT-003.reason — the codes design gave the three reasons. */
export type StockAdjustmentReason = "RECEIVE" | "SPOILED" | "RECOUNT";

/** One hand change of stock (ENT-003). `countedQty` is a recount's alone. */
export type StockAdjustment = {
  id: number;
  reason: StockAdjustmentReason;
  quantityDelta: number;
  countedQty: number | null;
  note: string | null;
  adjustedById: number;
  adjustedByName: string;
  adjustedAt: string;
};
export type StockAdjustmentPage = { items: StockAdjustment[]; page: number; pageSize: number; total: number };

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
  adjustments: StockAdjustmentPage;
};

/** API-019 · GET /api/products/{id}?historyPage=&adjustmentsPage= — null when unknown or no longer sold (PRODUCT_NOT_FOUND). */
export async function getProduct(id: number, historyPage = 1, adjustmentsPage = 1): Promise<ProductDetail | null> {
  const params = new URLSearchParams({ historyPage: String(historyPage), adjustmentsPage: String(adjustmentsPage) });
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

export type ProductDiscontinuing = "discontinued" | "gone" | "forbidden" | "signedOut";

/**
 * API-023 · POST /api/products/{id}/discontinue — ACTIVE → DISCONTINUED, the owner's alone (ACL-020): the api
 * answers anyone else 403 whatever the page showed. Already discontinued or never there is PRODUCT_NOT_FOUND.
 * Nothing is deleted — the bills that sold it keep its name and price (BR-talad-037@v1).
 */
export async function discontinueProduct(id: number): Promise<ProductDiscontinuing> {
  const response = await apiFetch(`/api/products/${id}/discontinue`, { method: "POST" });
  if (response.status === 204) return "discontinued";
  if (response.status === 401) return "signedOut";
  if (response.status === 403) return "forbidden";
  const body = (await response.json().catch(() => null)) as { code?: string } | null;
  if (body?.code === "PRODUCT_NOT_FOUND") return "gone";
  throw new Error(`POST /api/products/${id}/discontinue answered ${response.status}`);
}

/** API-024 body — numbers go as typed (null when empty or not a number); the rule is the domain's. */
export type StockAdjustmentBody = {
  reason: string | null;
  quantity: number | null;
  countedQty: number | null;
  note: string | null;
  requestKey: string;
};

/** The fields a refusal can sit under — the api's names. */
export type StockAdjustmentField = "reason" | "quantity" | "countedQty" | "requestKey";

export type StockAdjusting =
  | { ok: true; product: ProductDetail }
  | { ok: false; field: StockAdjustmentField; error: string }
  | { ok: false; duplicate: string }
  | { ok: false; refused: "gone" | "signedOut" | "forbidden" };

/**
 * API-024 · POST /api/products/{id}/stock-adjustments — one adjustment under the form's key. A refusal comes back
 * under its field (STOCK_ADJUSTMENT_INVALID); the same form saved already is STOCK_ADJUSTMENT_DUPLICATE with the
 * sentence of BR-talad-041@v1; a product discontinued since the page opened is PRODUCT_NOT_FOUND.
 */
export async function adjustStock(id: number, body: StockAdjustmentBody): Promise<StockAdjusting> {
  const response = await apiFetch(`/api/products/${id}/stock-adjustments`, { method: "POST", body: JSON.stringify(body) });
  if (response.status === 201) return { ok: true, product: (await response.json()) as ProductDetail };
  if (response.status === 401) return { ok: false, refused: "signedOut" };
  if (response.status === 403) return { ok: false, refused: "forbidden" };
  const answer = (await response.json().catch(() => null)) as { code?: string; errors?: { field: string; message: string }[] } | null;
  const first = answer?.errors?.[0];
  if (answer?.code === "PRODUCT_NOT_FOUND") return { ok: false, refused: "gone" };
  if (answer?.code === "STOCK_ADJUSTMENT_DUPLICATE" && first) return { ok: false, duplicate: first.message };
  if (answer?.code === "STOCK_ADJUSTMENT_INVALID" && first) return { ok: false, field: first.field as StockAdjustmentField, error: first.message };
  throw new Error(`POST /api/products/${id}/stock-adjustments answered ${response.status}`);
}
