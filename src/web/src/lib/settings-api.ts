import { apiFetch } from "./api-client";

// Shapes the api answers (FE-talad-029) — API-030 · API-031.

/** The % in force; never set = 0 with nobody and no time (UI-talad-017 state "empty"). */
export type MemberDiscount = { ratePercent: number; changedById: number | null; changedByName: string | null; changedAt: string | null };

/** API-030 · GET /api/settings/member-discount — the owner's alone. */
export async function getMemberDiscount(): Promise<MemberDiscount> {
  const response = await apiFetch("/api/settings/member-discount");
  if (!response.ok) throw new Error(`GET /api/settings/member-discount answered ${response.status}`);
  return (await response.json()) as MemberDiscount;
}

export type MemberDiscountChange =
  | { ok: true; current: MemberDiscount }
  | { ok: false; error: string }
  | { ok: false; refused: "signedOut" | "forbidden" };

/**
 * API-031 · POST — a new version. The rate goes as typed (null when the box is empty or not a number):
 * BR-talad-010@v1 is checked in the domain only (interfaces.json ruleEnforcement), and its sentence comes
 * back under `ratePercent`.
 */
export async function setMemberDiscount(ratePercent: number | null): Promise<MemberDiscountChange> {
  const response = await apiFetch("/api/settings/member-discount", { method: "POST", body: JSON.stringify({ ratePercent }) });
  if (response.status === 201) return { ok: true, current: (await response.json()) as MemberDiscount };
  if (response.status === 401) return { ok: false, refused: "signedOut" };
  if (response.status === 403) return { ok: false, refused: "forbidden" };
  if (response.status === 400) {
    const body = (await response.json()) as { errors: { field: string; message: string }[] };
    const error = body.errors.find((e) => e.field === "ratePercent")?.message;
    if (error) return { ok: false, error };
  }
  throw new Error(`POST /api/settings/member-discount answered ${response.status}`);
}
