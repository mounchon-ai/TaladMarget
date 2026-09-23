import { Suspense, type ReactNode } from "react";
import { redirect } from "next/navigation";
import { AppFrame } from "@/components/app-frame";
import { DeniedNotice } from "@/components/denied-notice";
import { MemberMissingNotice, RegisteredNotice } from "@/components/registered-notice";
import { getMe } from "@/lib/me";

// Every signed-in screen sits in this route group. A token the api no longer accepts ends the session
// instead of showing a frame with nothing in it.
export default async function FramedLayout({ children }: { children: ReactNode }) {
  const me = await getMe();
  if (!me) redirect("/logout");
  return (
    <AppFrame
      me={me}
      notice={
        <Suspense fallback={null}>
          <DeniedNotice />
          <RegisteredNotice />
          <MemberMissingNotice />
        </Suspense>
      }
    >
      {children}
    </AppFrame>
  );
}
