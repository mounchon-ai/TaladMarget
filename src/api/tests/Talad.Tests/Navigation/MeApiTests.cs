using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Application.Navigation;
using Talad.Domain.Accounts;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Navigation;

[Trait("feature", "FE-talad-003")]
public class MeApiTests : IClassFixture<TaladApiFactory>
{
    private readonly TaladApiFactory _factory;
    private readonly HttpClient _client;

    public MeApiTests(TaladApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        if (!db.UserAccounts.Any(a => a.Username == "somchai")) factory.Seed("somchai", "สมชาย", "Somchai#2569");
        if (!db.UserAccounts.Any(a => a.Username == "owner")) factory.Seed("owner", "เจ้าของร้าน", "Owner#2569", UserRole.Owner);
        if (!db.UserAccounts.Any(a => a.Username == "manee")) factory.Seed("manee", "มานี", "Manee#2569");
    }

    private async Task<string> TokenFor(string username, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    private async Task<HttpResponseMessage> GetMe(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    [Fact, Trait("ac", "AC-talad-044")]
    public async Task Cashier_menu_has_only_sales_members_and_history()
    {
        var response = await GetMe(await TokenFor("somchai", "Somchai#2569"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = (await response.Content.ReadFromJsonAsync<CurrentUser>())!;
        Assert.Equal("somchai", me.Username);
        Assert.Equal(["หน้าขาย", "สมาชิก", "ประวัติการขาย"], me.Menu.Select(m => m.Label));
    }

    [Fact, Trait("ac", "AC-talad-045")]
    public async Task Cashier_may_not_open_the_stock_screen()
    {
        var me = (await (await GetMe(await TokenFor("somchai", "Somchai#2569"))).Content.ReadFromJsonAsync<CurrentUser>())!;

        Assert.DoesNotContain("UI-talad-010", me.Screens);
        Assert.Contains("UI-talad-002", me.Screens);
    }

    [Fact]
    public async Task Owner_sees_every_menu_including_the_sales_work()
    {
        var me = (await (await GetMe(await TokenFor("owner", "Owner#2569"))).Content.ReadFromJsonAsync<CurrentUser>())!;

        Assert.Equal(
            ["หน้าขาย", "สมาชิก", "ประวัติการขาย", "สต็อก", "โปรโมชั่น", "ส่วนลดสมาชิก",
             "รายงานยอดขาย", "รายงานสินค้าขายดี", "รายงานยอดขายแยกตามผู้ขาย", "รายงานสต็อกคงเหลือ", "บัญชีพนักงาน"],
            me.Menu.Select(m => m.Label));
        Assert.Contains("UI-talad-010", me.Screens);
    }

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Me_without_a_token_answers_401()
    {
        var response = await _client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_disabled_account_is_refused_even_with_an_unexpired_token()
    {
        var token = await TokenFor("manee", "Manee#2569");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
            (await db.UserAccounts.SingleAsync(a => a.Username == "manee")).Disable();
            await db.SaveChangesAsync();
        }

        var response = await GetMe(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

[Trait("feature", "FE-talad-003")]
public class ScreenAccessTests
{
    [Fact, Trait("ac", "AC-talad-045")]
    public void Cashier_cannot_open_owner_only_screens()
    {
        Assert.False(ScreenAccess.CanOpen(UserRole.Cashier, "UI-talad-010"));
        Assert.False(ScreenAccess.CanOpen(UserRole.Cashier, "UI-talad-009"));
        Assert.False(ScreenAccess.CanOpen(UserRole.Cashier, "RPT-talad-002"));
        Assert.True(ScreenAccess.CanOpen(UserRole.Cashier, "UI-talad-008"));
    }

    [Fact]
    public void Owner_inherits_every_cashier_screen()
    {
        Assert.All(ScreenAccess.ScreensFor(UserRole.Cashier), s => Assert.True(ScreenAccess.CanOpen(UserRole.Owner, s)));
    }

    [Fact]
    public void Sign_in_is_in_nobodys_menu()
    {
        Assert.DoesNotContain(ScreenAccess.MenuFor(UserRole.Owner), m => m.Screen == "UI-talad-001");
        Assert.False(ScreenAccess.CanOpen(UserRole.Owner, "UI-talad-001"));
    }
}
