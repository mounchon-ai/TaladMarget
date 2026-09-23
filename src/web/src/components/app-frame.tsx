import type { ReactNode } from "react";
import type { Me } from "@/lib/me";
import { MENU_GROUPS, SCREEN_ROUTES, navTestId } from "@/lib/screen-routes";
import { NavLink } from "./nav-link";

type Props = { me: Me; notice?: ReactNode; children: ReactNode };

/**
 * The frame every screen but sign-in is drawn inside — top bar, left menu, footer (mock's
 * shell/index.html, reference · NFR-talad-002). The menu is exactly what the api says this role may open
 * (API-044 · AC-talad-044); a heading with nothing under it is not drawn.
 */
export function AppFrame({ me, notice, children }: Props) {
  const visible = new Map(me.menu.map((m) => [m.screen, m.label]));
  const groups = MENU_GROUPS.map((g) => ({ heading: g.heading, screens: g.screens.filter((s) => visible.has(s)) })).filter(
    (g) => g.screens.length > 0,
  );

  return (
    <div className="shell-app">
      <header className="appbar">
        <span className="wordmark">
          <span className="mark">ต</span>ตลาดมาร์เก็ต
        </span>
        <span className="spacer" />
        <span className="who">{me.displayName}</span>
        <form action="/logout" method="post">
          <button type="submit" className="signout" data-testid={navTestId("UI-talad-001")}>
            ออกจากระบบ
          </button>
        </form>
      </header>
      <div className="frame">
        <nav className="appnav" aria-label="เมนูหลัก">
          {groups.map((g) => (
            <div key={g.heading}>
              <div className="navhead">{g.heading}</div>
              {g.screens.map((s) => (
                <NavLink key={s} href={SCREEN_ROUTES[s]} testId={navTestId(s)}>
                  {visible.get(s)}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>
        <div className="content">
          <main>
            {notice}
            {children}
          </main>
        </div>
      </div>
      <footer className="appfoot">ตลาดมาร์เก็ต</footer>
    </div>
  );
}
