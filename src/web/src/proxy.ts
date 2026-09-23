import { NextResponse, type NextRequest } from "next/server";
import { SESSION_COOKIE, decideRoute } from "@/lib/auth-routing";

// BR-talad-005@v1 · AC-talad-036 — nobody reaches a page without signing in first, and signing in
// brings them back to the page they opened. Optimistic: only the cookie is read here; the api checks
// the JWT itself on every call (NFR-talad-005).
export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
  const decision = decideRoute(pathname, search, request.cookies.has(SESSION_COOKIE));
  return decision.kind === "redirect" ? NextResponse.redirect(new URL(decision.to, request.url)) : NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp|ico)$).*)"],
};
