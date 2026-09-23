using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Api.Sales;
using Talad.Application.Catalog;
using Talad.Application.Members;
using Talad.Application.Sales;
using Talad.Domain.Accounts;
using Talad.Domain.Members;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Members;

/// <summary>
/// UC-talad-009 · API-012 · API-008 — find a member at the sales screen and bind them to your own cart.
/// The ACs put สมหญิง on 0812345678 in different states, so every test gets its own database.
/// The member-discount total (ยอดที่ต้องชำระ) is API-009's, FE-talad-031 — not asserted here.
/// </summary>
[Trait("feature", "FE-talad-011")]
public sealed class MemberSearchAndBindTests : IDisposable
{
    private readonly TaladApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    // ── fixtures ────────────────────────────────────────────────────────────────────────────────────

    private async Task<HttpClient> SignedInAs(string username, string displayName)
    {
        _factory.Seed(username, displayName, "Pass#2569");
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "Pass#2569"));
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>A member registered by <paramref name="registrar"/> — ACTIVE, or hidden by the owner, with what they have bought.</summary>
    private int SeedMember(string name, string phone, string registrar = "manee", decimal accumulated = 0m, bool hidden = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        var by = db.UserAccounts.SingleOrDefault(a => a.Username == registrar);
        if (by is null)
        {
            by = new UserAccount(registrar, registrar, UserRole.Cashier, DateTimeOffset.UnixEpoch);
            by.SetPasswordHash("unused");
            db.UserAccounts.Add(by);
            db.SaveChanges();
        }
        var member = Member.Register(name, phone, by.Id, DateTimeOffset.UnixEpoch);
        db.Members.Add(member);
        // buying and hiding are other units' (FE-talad-033 · FE-talad-015) — set the columns as they would leave them
        db.Entry(member).Property(nameof(Member.AccumulatedAmount)).CurrentValue = accumulated;
        if (hidden)
        {
            db.Entry(member).Property(nameof(Member.Status)).CurrentValue = MemberStatus.Hidden;
            db.Entry(member).Property(nameof(Member.HiddenById)).CurrentValue = (int?)by.Id;
            db.Entry(member).Property(nameof(Member.HiddenAt)).CurrentValue = (DateTimeOffset?)DateTimeOffset.UnixEpoch;
        }
        db.SaveChanges();
        return member.Id;
    }

    private static async Task<PagedResult<MemberView>> Search(HttpClient client, string q) =>
        (await client.GetFromJsonAsync<PagedResult<MemberView>>($"/api/members?q={Uri.EscapeDataString(q)}"))!;

    private static Task<HttpResponseMessage> Bind(HttpClient client, int? memberId) =>
        client.PutAsJsonAsync("/api/cart/member", new SetMemberRequest(memberId));

    private static async Task<CartView> Cart(HttpClient client) => (await client.GetFromJsonAsync<CartView>("/api/cart"))!;

    // ── AC-talad-016 — the whole phone, then bind ───────────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-016")]
    public async Task Somchai_finds_somying_by_her_whole_phone_and_binds_her()
    {
        var somying = SeedMember("สมหญิง ใจดี", "0812345678", registrar: "manee", accumulated: 1000m);
        var somchai = await SignedInAs("somchai", "สมชาย");

        var page = await Search(somchai, "0812345678");
        var bound = await Bind(somchai, somying);

        // BR-talad-031@v1 — registered by มานี, seen by สมชาย: name, the whole phone, what she has bought
        var found = Assert.Single(page.Items);
        Assert.Equal((somying, "สมหญิง ใจดี", "0812345678", 1000m), (found.Id, found.Name, found.Phone, found.AccumulatedAmount));
        Assert.Equal(HttpStatusCode.OK, bound.StatusCode);
        var member = (await Cart(somchai)).Member;
        Assert.NotNull(member);
        Assert.Equal((somying, "สมหญิง ใจดี"), (member.Id, member.Name));
    }

    // ── AC-talad-017 — part of a name ───────────────────────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-017")]
    public async Task Part_of_a_name_lists_everyone_whose_name_has_it_and_somchai_binds_sommai()
    {
        SeedMember("สมหญิง ใจดี", "0812345678");
        var sommai = SeedMember("สมหมาย รักดี", "0898765432");
        SeedMember("มานะ ขยัน", "0861112222"); // not a match — "exactly two" has to mean something
        var somchai = await SignedInAs("somchai", "สมชาย");

        var page = await Search(somchai, "สมห");
        await Bind(somchai, sommai);

        Assert.Equal(["สมหญิง ใจดี", "สมหมาย รักดี"], page.Items.Select(m => m.Name));
        Assert.Equal(2, page.Total);
        Assert.Equal(sommai, (await Cart(somchai)).Member?.Id);
    }

    // ── AC-talad-018 · 019 — nothing found ──────────────────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-018")]
    public async Task A_phone_nobody_has_finds_nobody_and_the_cart_stays_unbound()
    {
        SeedMember("สมหญิง ใจดี", "0812345678");
        var somchai = await SignedInAs("somchai", "สมชาย");

        var page = await Search(somchai, "0899999999");

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
        Assert.Null((await Cart(somchai)).Member);
    }

    [Fact, Trait("ac", "AC-talad-019")]
    public async Task The_last_four_digits_of_a_phone_find_nobody()
    {
        SeedMember("สมหญิง ใจดี", "0812345678");
        var somchai = await SignedInAs("somchai", "สมชาย");

        var page = await Search(somchai, "5678");

        Assert.Empty(page.Items);
        Assert.Null((await Cart(somchai)).Member);
    }

    [Fact]
    public async Task A_phone_typed_with_dashes_is_still_the_whole_phone()
    {
        SeedMember("สมหญิง ใจดี", "0812345678");
        var somchai = await SignedInAs("somchai", "สมชาย");

        var page = await Search(somchai, "081-234-5678");

        Assert.Equal(["สมหญิง ใจดี"], page.Items.Select(m => m.Name));
    }

    [Fact]
    public async Task No_term_lists_every_active_member_20_a_page_by_name()
    {
        for (var i = 0; i < 21; i++) SeedMember($"สมาชิก {i:D2}", $"08000000{i:D2}");
        SeedMember("ซ่อนแล้ว", "0899999999", hidden: true);
        var somchai = await SignedInAs("somchai", "สมชาย");

        var first = await Search(somchai, "");
        var second = (await somchai.GetFromJsonAsync<PagedResult<MemberView>>("/api/members?page=2"))!;

        Assert.Equal((20, 21, 20), (first.Items.Count, first.Total, first.PageSize));
        Assert.Equal("สมาชิก 00", first.Items[0].Name);
        Assert.Equal(["สมาชิก 20"], second.Items.Select(m => m.Name));
    }

    // ── AC-talad-012 — BR-talad-040@v2: a hidden member is not found and not bound ──────────────────

    [Fact, Trait("ac", "AC-talad-012")]
    public async Task Hidden_somying_is_found_neither_by_phone_nor_by_name_and_cannot_be_bound()
    {
        var somying = SeedMember("สมหญิง ใจดี", "0812345678", accumulated: 250m, hidden: true);
        var somchai = await SignedInAs("somchai", "สมชาย");

        var byPhone = await Search(somchai, "0812345678");
        var byName = await Search(somchai, "สมหญิง");
        var bind = await Bind(somchai, somying);

        Assert.Empty(byPhone.Items);
        Assert.Empty(byName.Items);
        Assert.Equal(HttpStatusCode.NotFound, bind.StatusCode);
        Assert.Equal("MEMBER_NOT_FOUND", (await bind.Content.ReadFromJsonAsync<CartError>())!.Code);
        Assert.Null((await Cart(somchai)).Member);
    }

    [Fact]
    public async Task A_member_id_that_does_not_exist_is_not_found()
    {
        var somchai = await SignedInAs("somchai", "สมชาย");

        var bind = await Bind(somchai, 999_999);

        Assert.Equal(HttpStatusCode.NotFound, bind.StatusCode);
        Assert.Null((await Cart(somchai)).Member);
    }

    // ── API-008 — unbind, and only ever your own cart (BR-talad-020@v1) ────────────────────────────

    [Fact]
    public async Task Binding_nobody_unbinds_the_member()
    {
        var somying = SeedMember("สมหญิง ใจดี", "0812345678");
        var somchai = await SignedInAs("somchai", "สมชาย");
        await Bind(somchai, somying);

        var unbound = await Bind(somchai, null);

        Assert.Equal(HttpStatusCode.OK, unbound.StatusCode);
        Assert.Null((await Cart(somchai)).Member);
    }

    [Fact]
    public async Task Manee_binding_a_member_lands_on_her_own_cart_and_not_on_somchais()
    {
        var somying = SeedMember("สมหญิง ใจดี", "0812345678");
        var somchai = await SignedInAs("somchai", "สมชาย");
        var manee = await SignedInAs("manee-seller", "มานี");
        await Cart(somchai);

        await Bind(manee, somying);

        Assert.Equal(somying, (await Cart(manee)).Member?.Id);
        Assert.Null((await Cart(somchai)).Member);
    }

    // ── NFR-talad-005 ───────────────────────────────────────────────────────────────────────────────

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Without_a_token_search_and_bind_answer_401()
    {
        var anonymous = _factory.CreateClient();

        var search = await anonymous.GetAsync("/api/members?q=0812345678");
        var bind = await Bind(anonymous, 1);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (search.StatusCode, bind.StatusCode));
    }
}
