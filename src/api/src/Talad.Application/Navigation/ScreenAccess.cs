using Talad.Domain.Accounts;

namespace Talad.Application.Navigation;

public sealed record MenuItem(string Screen, string Label);

/// <summary>
/// FUN-talad-010 · BR-talad-018@v1 / BR-talad-021@v1 — which screens a role may open, and the left-menu
/// entries it sees. Copied from design: <c>screens.json</c> <c>roles</c> per screen, and the order and label of
/// the top-level nodes in <c>sitemap.json</c>. ROLE-002 (Owner) inherits ROLE-001 (Cashier) in
/// <c>rbac.json</c>, which is why every cashier screen is open to the owner too.
/// UI-talad-001 (kind "auth") is drawn outside the frame and is in nobody's menu.
/// </summary>
public static class ScreenAccess
{
    private sealed record Screen(string Id, string? Parent, string Label, bool Cashier);

    // Order is sitemap order; Cashier=true means screens.json lists ROLE-001 for it.
    private static readonly Screen[] Screens =
    [
        new("UI-talad-002", null, "หน้าขาย", true),
        new("UI-talad-003", "UI-talad-002", "เลือกโปรโมชั่น", true),
        new("RPT-talad-001", "UI-talad-002", "ใบเสร็จ", true),
        new("UI-talad-004", null, "สมาชิก", true),
        new("UI-talad-005", "UI-talad-004", "สมัครสมาชิก", true),
        new("UI-talad-006", "UI-talad-004", "ข้อมูลสมาชิก", true),
        new("UI-talad-007", null, "ประวัติการขาย", true),
        new("UI-talad-008", "UI-talad-007", "รายละเอียดบิล", true),
        new("UI-talad-009", "UI-talad-008", "ยกเลิกบิล", false),
        new("UI-talad-010", null, "สต็อก", false),
        new("UI-talad-011", "UI-talad-010", "เพิ่ม/แก้ไขสินค้า", false),
        new("UI-talad-012", "UI-talad-010", "รายละเอียดสินค้า", false),
        new("UI-talad-013", "UI-talad-012", "แก้ราคา", false),
        new("UI-talad-014", "UI-talad-012", "ปรับสต็อก", false),
        new("UI-talad-015", null, "โปรโมชั่น", false),
        new("UI-talad-016", "UI-talad-015", "เพิ่ม/แก้ไขโปรโมชั่น", false),
        new("UI-talad-017", null, "ส่วนลดสมาชิก", false),
        new("RPT-talad-002", null, "รายงานยอดขาย", false),
        new("RPT-talad-003", null, "รายงานสินค้าขายดี", false),
        new("RPT-talad-004", null, "รายงานยอดขายแยกตามผู้ขาย", false),
        new("RPT-talad-005", null, "รายงานสต็อกคงเหลือ", false),
        new("UI-talad-018", null, "บัญชีพนักงาน", false),
        new("UI-talad-019", "UI-talad-018", "เพิ่ม/แก้ไขบัญชี", false),
    ];

    private static bool Opens(UserRole role, Screen s) => role == UserRole.Owner || s.Cashier;

    public static IReadOnlyList<string> ScreensFor(UserRole role) =>
        Screens.Where(s => Opens(role, s)).Select(s => s.Id).ToList();

    public static IReadOnlyList<MenuItem> MenuFor(UserRole role) =>
        Screens.Where(s => s.Parent is null && Opens(role, s)).Select(s => new MenuItem(s.Id, s.Label)).ToList();

    public static bool CanOpen(UserRole role, string screen) =>
        Screens.Any(s => s.Id == screen && Opens(role, s));
}
