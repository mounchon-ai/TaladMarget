import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";
import { apiBaseUrl } from "@/lib/auth-api";
import { LOGIN_PATH, SESSION_COOKIE } from "@/lib/auth-routing";

// API-002 · sign out. POST is the frame's "ออกจากระบบ"; GET is where a session the api refused is sent
// (expired token, disabled account). Either way the cookie goes and the person lands on sign-in; a cart
// they left open stays on the server (BR-talad-020@v1).
async function signOut(request: NextRequest) {
  const store = await cookies();
  const token = store.get(SESSION_COOKIE)?.value;
  if (token) {
    await fetch(`${apiBaseUrl()}/api/auth/logout`, {
      method: "POST",
      headers: { authorization: `Bearer ${token}` },
      cache: "no-store",
    }).catch(() => undefined); // the cookie is cleared whether or not the api could be reached
  }
  store.delete(SESSION_COOKIE);
  return NextResponse.redirect(new URL(LOGIN_PATH, request.url), 303);
}

export const GET = signOut;
export const POST = signOut;
