import { cookies } from "next/headers";
import { apiBaseUrl } from "./auth-api";
import { SESSION_COOKIE } from "./auth-routing";

/** Every call after sign-in carries the session's JWT — the api refuses anything else (NFR-talad-005). */
export async function apiFetch(path: string, init: RequestInit = {}): Promise<Response> {
  const token = (await cookies()).get(SESSION_COOKIE)?.value;
  const headers = new Headers(init.headers);
  if (token) headers.set("authorization", `Bearer ${token}`);
  if (init.body) headers.set("content-type", "application/json");
  return fetch(`${apiBaseUrl()}${path}`, { ...init, headers, cache: "no-store" });
}
