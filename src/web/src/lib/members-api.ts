import { apiFetch } from "./api-client";
import type { MemberFieldErrors } from "./member-form";

// Shapes the api answers (FE-talad-009 · 011 · 013) — API-012 · API-013 · API-014 · API-015.

export type Member = { id: number; name: string; phone: string; accumulatedAmount: number; status: string };
export type MemberPage = { items: Member[]; page: number; pageSize: number; total: number };

/**
 * API-012 · GET /api/members?q=&page= — ACTIVE members by the whole phone or part of the name, 20 a page.
 * The api decides what matches (BR-talad-004@v1 at api · domain); the term goes as typed.
 */
export async function searchMembers(q: string, page = 1): Promise<MemberPage> {
  const params = new URLSearchParams({ page: String(page) });
  if (q) params.set("q", q);
  const response = await apiFetch(`/api/members?${params}`);
  if (!response.ok) throw new Error(`GET /api/members answered ${response.status}`);
  return (await response.json()) as MemberPage;
}

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

/** API-014 · GET /api/members/{id} — null when the member does not exist or is hidden (MEMBER_NOT_FOUND). */
export async function getMember(id: number): Promise<Member | null> {
  const response = await apiFetch(`/api/members/${id}`);
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`GET /api/members/${id} answered ${response.status}`);
  return (await response.json()) as Member;
}

export type MemberEdit =
  | { ok: true; member: Member }
  | { ok: false; errors: MemberFieldErrors }
  | { ok: false; gone: true }
  | { ok: false; signedOut: true };

/**
 * API-015 · PUT /api/members/{id} — the whole record, name and phone both, every time (a name-only edit
 * sends the phone that was already there). 400 · 409 carry field errors; a member hidden since the page
 * opened is told apart by `code`, not by status or message.
 */
export async function editMember(id: number, name: string, phone: string): Promise<MemberEdit> {
  const response = await apiFetch(`/api/members/${id}`, { method: "PUT", body: JSON.stringify({ name, phone }) });
  if (response.ok) return { ok: true, member: (await response.json()) as Member };
  if (response.status === 401) return { ok: false, signedOut: true };
  const body = (await response.json().catch(() => null)) as MemberError | null;
  if (body?.code === "MEMBER_NOT_FOUND") return { ok: false, gone: true };
  if (body && (response.status === 400 || response.status === 409)) {
    const errors: MemberFieldErrors = {};
    for (const e of body.errors) {
      if (e.field === "name" || e.field === "phone") errors[e.field] = e.message;
    }
    return { ok: false, errors };
  }
  throw new Error(`PUT /api/members/${id} answered ${response.status}`);
}
