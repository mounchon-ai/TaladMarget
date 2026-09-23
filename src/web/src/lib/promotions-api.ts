import { apiFetch } from "./api-client";
import type { PromotionFieldErrors, PromotionTermsBody } from "./promotion-form";
import { PROMOTION_FIELDS } from "./promotion-form";

// Shapes the api answers (FE-talad-025) — API-025 · API-026 · API-027 · API-028.

export type ProductRef = { id: number; name: string };

/** A promotion with the conditions in force. `status` is the api's enum name ("Active"); `type` is ENT-006's code. */
export type Promotion = {
  id: number;
  status: string;
  versionId: number;
  name: string;
  type: string;
  productA: ProductRef | null;
  qtyA: number | null;
  productB: ProductRef | null;
  qtyB: number | null;
  freeProduct: ProductRef | null;
  freeQty: number | null;
  ratePercent: number | null;
  minSubtotal: number | null;
  startDate: string;
  endDate: string | null;
  changedAt: string;
};
export type PromotionPage = { items: Promotion[]; page: number; pageSize: number; total: number };

/** API-025 · GET /api/promotions?search=&page= — ACTIVE promotions by part of the name, 20 a page. */
export async function searchPromotions(search: string, page = 1): Promise<PromotionPage> {
  const params = new URLSearchParams({ page: String(page) });
  if (search) params.set("search", search);
  const response = await apiFetch(`/api/promotions?${params}`);
  if (!response.ok) throw new Error(`GET /api/promotions answered ${response.status}`);
  return (await response.json()) as PromotionPage;
}

/** API-026 · GET /api/promotions/{id} — null when it does not exist or is discontinued (PROMOTION_NOT_FOUND). */
export async function getPromotion(id: number): Promise<Promotion | null> {
  const response = await apiFetch(`/api/promotions/${id}`);
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`GET /api/promotions/${id} answered ${response.status}`);
  return (await response.json()) as Promotion;
}

export type PromotionSave =
  | { ok: true; promotion: Promotion }
  | { ok: false; errors: PromotionFieldErrors }
  | { ok: false; refused: "gone" | "signedOut" | "forbidden" };

type PromotionError = { code: string; errors: { field: string; message: string }[] };

/**
 * API-027 · POST /api/promotions (new) or API-028 · POST /api/promotions/{id}/versions (edit) — the whole set
 * of terms either way. An edit is a new version in force from now; the one before stays for the bills that
 * used it (BR-talad-035@v1 · BR-talad-036@v1). Every rule is the domain's (interfaces.json ruleEnforcement),
 * so a refusal comes back as PROMOTION_INVALID with each message under its ENT-006 field.
 */
export async function savePromotion(id: number | null, terms: PromotionTermsBody): Promise<PromotionSave> {
  const path = id === null ? "/api/promotions" : `/api/promotions/${id}/versions`;
  const response = await apiFetch(path, { method: "POST", body: JSON.stringify(terms) });
  if (response.status === 201) return { ok: true, promotion: (await response.json()) as Promotion };
  if (response.status === 401) return { ok: false, refused: "signedOut" };
  if (response.status === 403) return { ok: false, refused: "forbidden" };
  const body = (await response.json().catch(() => null)) as PromotionError | null;
  if (body?.code === "PROMOTION_NOT_FOUND") return { ok: false, refused: "gone" };
  if (body?.code === "PROMOTION_INVALID") {
    const errors: PromotionFieldErrors = {};
    for (const e of body.errors) {
      if ((PROMOTION_FIELDS as readonly string[]).includes(e.field)) errors[e.field as keyof PromotionFieldErrors] = e.message;
    }
    return { ok: false, errors };
  }
  throw new Error(`POST ${path} answered ${response.status}`);
}
