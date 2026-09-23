import type { Metadata } from "next";
import { LoginForm } from "@/components/login-form";
import { safeNext } from "@/lib/auth-routing";
import { signIn } from "./actions";

export const metadata: Metadata = { title: "เข้าสู่ระบบ · ตลาดมาร์เก็ต" };

// UI-talad-001 · kind "auth" — drawn outside the app frame (gate 66), open to ROLE-003 only.
export default async function LoginPage({ searchParams }: { searchParams: Promise<{ next?: string }> }) {
  const { next } = await searchParams;
  return (
    <main className="authwrap" data-screen="UI-talad-001">
      <LoginForm action={signIn} next={safeNext(next)} />
    </main>
  );
}
