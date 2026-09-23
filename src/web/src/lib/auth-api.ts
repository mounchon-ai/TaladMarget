// API-001 · POST /api/auth/login — the web app reaches the api and nothing else (stack may_reach: web → api).

export type LoginOk = { ok: true; accessToken: string; expiresAt: string };
export type LoginFailed = {
  ok: false;
  code: "USER_NOT_FOUND" | "WRONG_PASSWORD" | "ACCOUNT_DISABLED" | "NETWORK" | "UNEXPECTED";
  message: string | null;
};
export type LoginOutcome = LoginOk | LoginFailed;

export function apiBaseUrl(): string {
  return process.env.TALAD_API_URL ?? "http://localhost:5010";
}

export async function requestLogin(
  username: string,
  password: string,
  fetchImpl: typeof fetch = fetch,
): Promise<LoginOutcome> {
  let response: Response;
  try {
    response = await fetchImpl(`${apiBaseUrl()}/api/auth/login`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ username, password }),
      cache: "no-store",
    });
  } catch {
    return { ok: false, code: "NETWORK", message: null };
  }

  if (response.ok) {
    const body = (await response.json()) as { accessToken: string; expiresAt: string };
    return { ok: true, accessToken: body.accessToken, expiresAt: body.expiresAt };
  }
  if (response.status === 401) {
    const body = (await response.json()) as { code: LoginFailed["code"]; message: string | null };
    return { ok: false, code: body.code, message: body.message };
  }
  return { ok: false, code: "UNEXPECTED", message: null };
}
