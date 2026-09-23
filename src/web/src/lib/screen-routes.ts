// Where each design screen lives in this app. The URL is dev's choice (design names screens, not paths);
// every page unit adds its screen here, and the menu and the permission check read this one table.

export const SCREEN_ROUTES: Readonly<Record<string, string>> = {
  "UI-talad-002": "/",
  "UI-talad-004": "/members",
  "UI-talad-005": "/members/new", // opened from UI-talad-004, not from the menu
  "UI-talad-007": "/sales",
  "UI-talad-010": "/stock",
  "UI-talad-015": "/promotions",
  "UI-talad-017": "/member-discount",
  "RPT-talad-002": "/reports/sales",
  "RPT-talad-003": "/reports/top-products",
  "RPT-talad-004": "/reports/by-seller",
  "RPT-talad-005": "/reports/stock",
  "UI-talad-018": "/staff",
};

/**
 * The menu heading each top-level screen sits under — the grouping mock's frame draws
 * (shell/index.html, reference). A heading with nothing visible under it is not drawn.
 */
export const MENU_GROUPS: ReadonlyArray<{ heading: string; screens: readonly string[] }> = [
  { heading: "งานขาย", screens: ["UI-talad-002", "UI-talad-004", "UI-talad-007"] },
  { heading: "สินค้า", screens: ["UI-talad-010", "UI-talad-015", "UI-talad-017"] },
  { heading: "รายงาน", screens: ["RPT-talad-002", "RPT-talad-003", "RPT-talad-004", "RPT-talad-005"] },
  { heading: "ตั้งค่า", screens: ["UI-talad-018"] },
];

/** mock's frame names every link `nav-<screen id in lower case>` (shell/index.html). */
export const navTestId = (screen: string) => `nav-${screen.toLowerCase()}`;
