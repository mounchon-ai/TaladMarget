"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { requestLogin } from "@/lib/auth-api";
import { SESSION_COOKIE, safeNext } from "@/lib/auth-routing";
import { toFormState, type LoginFormState } from "@/lib/login-errors";

// UI-talad-001 action "sign-in" → API-001. On success the JWT lives in an httpOnly cookie the page
// scripts never see, and the person goes where they meant to go (AC-talad-033 · AC-talad-036).
export async function signIn(_prev: LoginFormState, formData: FormData): Promise<LoginFormState> {
  const username = String(formData.get("username") ?? "");
  const password = String(formData.get("password") ?? "");

  const outcome = await requestLogin(username, password);
  if (!outcome.ok) return toFormState(outcome);

  (await cookies()).set(SESSION_COOKIE, outcome.accessToken, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    expires: new Date(outcome.expiresAt),
  });
  redirect(safeNext(formData.get("next")?.toString()));
}
