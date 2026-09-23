// BR-talad-005@v1 · AC-talad-036 — where a request goes before any page renders.
// Pure functions so the proxy and the sign-in action decide the same way and a test can ask them directly.

export const LOGIN_PATH = "/login";
/** Ends the session (API-002) and clears the cookie — open with or without a session. */
export const LOGOUT_PATH = "/logout";
/** UI-talad-002 หน้าขาย — the page a sign-in lands on when nothing else was asked for. */
export const HOME_PATH = "/";
export const SESSION_COOKIE = "talad_session";

/**
 * The `next` a sign-in may return to: only a path inside this app. Anything else — an absolute URL,
 * a protocol-relative `//host`, a backslash trick — falls back to the sales page, so the login form
 * can never be used to bounce someone to another site.
 */
export function safeNext(next: string | null | undefined): string {
  if (!next || !next.startsWith("/") || next.startsWith("//") || next.startsWith("/\\")) return HOME_PATH;
  return next;
}

export type RouteDecision = { kind: "pass" } | { kind: "redirect"; to: string };

/**
 * Optimistic check only (cookie present or not) — the api verifies the token on every call (NFR-talad-005).
 * - not signed in, any page but sign-in → sign-in, remembering where they were going, with no message
 * - already signed in and opening sign-in → the sales page (UI-talad-001 state "unauthorized")
 */
export function decideRoute(pathname: string, search: string, hasSession: boolean): RouteDecision {
  if (pathname === LOGOUT_PATH) return { kind: "pass" };
  if (pathname === LOGIN_PATH) {
    return hasSession ? { kind: "redirect", to: HOME_PATH } : { kind: "pass" };
  }
  if (hasSession) return { kind: "pass" };
  return { kind: "redirect", to: `${LOGIN_PATH}?next=${encodeURIComponent(pathname + search)}` };
}
