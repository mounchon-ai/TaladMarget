import { cache } from "react";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { apiBaseUrl } from "./auth-api";
import { SESSION_COOKIE } from "./auth-routing";
import { DENIED_PARAM } from "./denied";

export type MenuItem = { screen: string; label: string };
export type Me = { username: string; displayName: string; role: string; menu: MenuItem[]; screens: string[] };

/**
 * API-044 · GET /api/me with the session's JWT. `null` means the api refused the token (expired,
 * wrongly signed, account disabled) — the caller signs the person out. React.cache makes the frame and
 * the page share one call per request.
 */
export const getMe = cache(async (fetchImpl: typeof fetch = fetch): Promise<Me | null> => {
  const token = (await cookies()).get(SESSION_COOKIE)?.value;
  if (!token) return null;
  const response = await fetchImpl(`${apiBaseUrl()}/api/me`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  if (response.status === 401) return null;
  if (!response.ok) throw new Error(`GET /api/me answered ${response.status}`);
  return (await response.json()) as Me;
});

/** Pure: may this person open that screen? */
export const canOpen = (me: Me, screen: string) => me.screens.includes(screen);

/**
 * AC-talad-045 — every page calls this with its own screen id before it reads any data. A screen the
 * role may not open sends the person to the sales page with the no-permission notice; nothing of the
 * page is rendered. The api refuses the data as well (NFR-talad-005 · BR-talad-018@v1).
 */
export async function requireScreen(screen: string): Promise<Me> {
  const me = await getMe();
  if (!me) redirect("/logout");
  if (!canOpen(me, screen)) redirect(`/?${DENIED_PARAM}=1`);
  return me;
}
