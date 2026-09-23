using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Api.Members;
using Talad.Api.Sales;
using Talad.Application.Catalog;
using Talad.Application.Members;
using Talad.Domain.Accounts;
using Talad.Domain.Members;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Members;

/// <summary>
/// UC-talad-010 · API-016 — the owner hides a member: never deleted, not found by search or bind any more,
/// and the phone is free for someone new (BR-talad-040@v2). A seller cannot (BR-talad-019@v1).
/// Old bills showing the hidden member (AC-talad-013) need sales, which do not exist yet — here the row
/// that those bills will point at is shown to survive intact.
/// </summary>
[Trait("feature", "FE-talad-015")]
public sealed class MemberHideTests : IDisposable
{
    private readonly TaladApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    // ── fixtures ────────────────────────────────────────────────────────────────────────────────────

    private async Task<HttpClient> SignedInAs(string username, string displayName, UserRole role)
    {
        _factory.Seed(username, displayName, "Pass#2569", role);
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "Pass#2569"));
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<HttpClient> Owner() => SignedInAs("owner", "เจ้าของร้าน", UserRole.Owner);
    private Task<HttpClient> Somchai() => SignedInAs("somchai", "สมชาย", UserRole.Cashier);

    private async Task<int> Register(HttpClient client, string name, string phone)
    {
        var response = await client.PostAsJsonAsync("/api/members", new RegisterMemberRequest(name, phone));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MemberView>())!.Id;
    }

    private static Task<HttpResponseMessage> Hide(HttpClient client, int id) => client.PostAsync($"/api/members/{id}/hide", null);

    private static async Task<IReadOnlyList<MemberView>> Search(HttpClient client, string q) =>
        (await client.GetFromJsonAsync<PagedResult<MemberView>>($"/api/members?q={Uri.EscapeDataString(q)}"))!.Items;

    private Member Stored(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TaladDbContext>().Members.Single(m => m.Id == id);
    }

    private int AccountId(string username)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TaladDbContext>().UserAccounts.Single(a => a.Username == username).Id;
    }

    // ── AC-talad-012 · 089 — hidden: search and bind find nobody ────────────────────────────────────

    [Theory, Trait("ac", "AC-talad-012"), Trait("ac", "AC-talad-089")]
    [InlineData("สมหญิง ใจดี", "0812345678", "สมหญิง")] // AC-talad-012
    [InlineData("ปรีชา ดีงาม", "0861112222", "ปรีชา")]  // AC-talad-089
    public async Task The_owner_hides_a_member_and_search_by_phone_or_name_and_bind_find_nobody(string name, string phone, string part)
    {
        var owner = await Owner();
        var somchai = await Somchai();
        var id = await Register(somchai, name, phone);

        var hide = await Hide(owner, id);

        Assert.Equal(HttpStatusCode.NoContent, hide.StatusCode);
        Assert.Empty(await Search(somchai, phone));
        Assert.Empty(await Search(somchai, part));
        var bind = await somchai.PutAsJsonAsync("/api/cart/member", new SetMemberRequest(id));
        Assert.Equal(HttpStatusCode.NotFound, bind.StatusCode);
    }

    // ── AC-talad-013 — hidden, not deleted ──────────────────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-013")]
    public async Task Hiding_keeps_the_row_whole_with_who_hid_it_and_when()
    {
        var owner = await Owner();
        var id = await Register(owner, "สมหญิง ใจดี", "0812345678");

        await Hide(owner, id);

        var hidden = Stored(id);
        Assert.Equal(("สมหญิง ใจดี", "0812345678", MemberStatus.Hidden), (hidden.Name, hidden.Phone, hidden.Status));
        Assert.Equal(AccountId("owner"), hidden.HiddenById);
        Assert.NotNull(hidden.HiddenAt);
    }

    // ── AC-talad-090 — the phone is free again ──────────────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-090")]
    public async Task After_hiding_preecha_his_phone_registers_a_new_member_at_0()
    {
        var owner = await Owner();
        var somchai = await Somchai();
        var before = await Register(somchai, "ปรีชา ดีงาม", "0861112222");
        await Hide(owner, before);

        var again = await somchai.PostAsJsonAsync("/api/members", new RegisterMemberRequest("ปรีชา ดีงาม", "0861112222"));

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        var found = Assert.Single(await Search(somchai, "0861112222"));
        Assert.Equal(("ปรีชา ดีงาม", 0m), (found.Name, found.AccumulatedAmount));
        Assert.NotEqual(before, found.Id);
    }

    // ── BR-talad-019@v1 — the owner's alone ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_seller_is_refused_and_the_member_stays_active()
    {
        var somchai = await Somchai();
        var id = await Register(somchai, "สมหญิง ใจดี", "0812345678");

        var hide = await Hide(somchai, id);

        Assert.Equal(HttpStatusCode.Forbidden, hide.StatusCode);
        Assert.Equal(MemberStatus.Active, Stored(id).Status);
        Assert.Single(await Search(somchai, "0812345678"));
    }

    [Fact]
    public async Task Hiding_twice_or_hiding_nobody_is_not_found()
    {
        var owner = await Owner();
        var id = await Register(owner, "สมหญิง ใจดี", "0812345678");
        await Hide(owner, id);

        var twice = await Hide(owner, id);
        var nobody = await Hide(owner, 999_999);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (twice.StatusCode, nobody.StatusCode));
        Assert.Equal("MEMBER_NOT_FOUND", (await twice.Content.ReadFromJsonAsync<MemberError>())!.Code);
    }

    [Fact]
    public void The_domain_refuses_a_seller_even_without_the_endpoint_policy()
    {
        var seller = new UserAccount("somchai", "สมชาย", UserRole.Cashier, DateTimeOffset.UnixEpoch);
        var member = Member.Register("สมหญิง ใจดี", "0812345678", 1, DateTimeOffset.UnixEpoch);

        Assert.Throws<OwnerOnlyException>(() => member.Hide(seller, DateTimeOffset.UnixEpoch));
        Assert.Equal(MemberStatus.Active, member.Status);
    }

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Without_a_token_hiding_answers_401()
    {
        var owner = await Owner();
        var id = await Register(owner, "สมหญิง ใจดี", "0812345678");

        var hide = await Hide(_factory.CreateClient(), id);

        Assert.Equal(HttpStatusCode.Unauthorized, hide.StatusCode);
        Assert.Equal(MemberStatus.Active, Stored(id).Status);
    }
}
