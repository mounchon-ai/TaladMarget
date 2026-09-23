"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";

/** One left-menu link; marks itself as the current page the way mock's frame does (aria-current). */
export function NavLink({ href, testId, children }: { href: string; testId: string; children: ReactNode }) {
  const pathname = usePathname();
  const current = href === "/" ? pathname === "/" : pathname === href || pathname.startsWith(`${href}/`);
  return (
    <Link href={href} data-testid={testId} aria-current={current ? "page" : undefined}>
      {children}
    </Link>
  );
}
