import { apiFetch } from "./api-client";
import type { MemberFieldErrors } from "./member-form";

// Shapes the api answers (FE-talad-009) — API-013.

export type Member = { id: number; name: string; phone: string; accumulatedAmount: number; status: string };

type MemberError = { code: string; errors: { field: string; message: string }[] };

export type Registration =
  | { ok: true; member: Member }
  | { ok: false; errors: MemberFieldErrors }
  | { ok: false; signedOut: true };

/**
 * API-013 · POST /api/members. 400 (BR-talad-002@v1) and 409 (BR-talad-030@v1) share one body, every
 * message under the field it is about. Anything else the api answers is thrown — the caller says
 * "try again" rather than guess.
 */
export async function registerMember(name: string, phone: string): Promise<Registration> {
  const response = await apiFetch("/api/members", { method: "POST", body: JSON.stringify({ name, phone }) });
  if (response.status === 201) return { ok: true, member: (await response.json()) as Member };
  if (response.status === 401) return { ok: false, signedOut: true };
  if (response.status === 400 || response.status === 409) {
    const body = (await response.json()) as MemberError;
    const errors: MemberFieldErrors = {};
    for (const e of body.errors) {
      if (e.field === "name" || e.field === "phone") errors[e.field] = e.message;
    }
    return { ok: false, errors };
  }
  throw new Error(`POST /api/members answered ${response.status}`);
}
