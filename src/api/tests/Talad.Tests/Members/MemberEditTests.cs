using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Api.Members;
using Talad.Application.Catalog;
using Talad.Application.Members;
using Talad.Domain.Accounts;
using Talad.Domain.Members;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Members;

/// <summary>
/// UC-talad-008 · API-014 · API-015 — open one member and give them a new name or phone, under the same
/// rules as registering. Every test gets its own database (the ACs reuse 0812345678).
/// </summary>
[Trait("feature", "FE-talad-013")]
public sealed class MemberEditTests : IDisposable
{
    private readonly TaladApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    // ── fixtures ────────────────────────────────────────────────────────────────────────────────────

    private async Task<HttpClient> Somchai()
    {
        _factory.Seed("somchai", "สมชาย", "Pass#2569");
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("somchai", "Pass#2569"));
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private int SeedMember(string name, string phone, decimal accumulated = 0m, bool hidden = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        var by = db.UserAccounts.SingleOrDefault(a => a.Username == "manee");
        if (by is null)
        {
            by = new UserAccount("manee", "มานี", UserRole.Cashier, DateTimeOffset.UnixEpoch);
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

    private Member Stored(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TaladDbContext>().Members.Single(m => m.Id == id);
    }

    private static Task<HttpResponseMessage> Edit(HttpClient client, int id, string? name, string? phone) =>
        client.PutAsJsonAsync($"/api/members/{id}", new EditMemberRequest(name, phone));

    private static async Task<(string Field, string Message)[]> Errors(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<MemberError>())!.Errors.Select(e => (e.Field, e.Message)).ToArray();

    // ── API-014 ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Opening_a_member_shows_name_whole_phone_and_what_they_bought()
    {
        var somying = SeedMember("สมหญิง ใจดี", "0812345678", accumulated: 1500m);
        var somchai = await Somchai();

        var member = (await somchai.GetFromJsonAsync<MemberView>($"/api/members/{somying}"))!;

        Assert.Equal((somying, "สมหญิง ใจดี", "0812345678", 1500m, "Active"), (member.Id, member.Name, member.Phone, member.AccumulatedAmount, member.Status));
    }

    [Fact]
    public async Task A_hidden_or_unknown_member_is_not_found()
    {
        var hidden = SeedMember("สมหญิง ใจดี", "0812345678", hidden: true);
        var somchai = await Somchai();

        var openHidden = await somchai.GetAsync($"/api/members/{hidden}");
        var openUnknown = await somchai.GetAsync("/api/members/999999");
        var editHidden = await Edit(somchai, hidden, "สมหญิง ใจงาม", "0812345678");

        Assert.Equal(
            (HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound),
            (openHidden.StatusCode, openUnknown.StatusCode, editHidden.StatusCode));
        Assert.Equal("MEMBER_NOT_FOUND", (await editHidden.Content.ReadFromJsonAsync<MemberError>())!.Code);
        Assert.Equal(("สมหญิง ใจดี", MemberStatus.Hidden), (Stored(hidden).Name, Stored(hidden).Status));
    }

    // ── AC-talad-043 — a seller renames; the phone stays theirs ─────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-043")]
    public async Task Somchai_renames_somying_and_the_sale_screen_search_finds_the_new_name()
    {
        var somying = SeedMember("สมหญิง ใจดี", "0812345678", accumulated: 1500m);
        var somchai = await Somchai();

        var response = await Edit(somchai, somying, "สมหญิง ใจงาม", "0812345678");
        var found = (await somchai.GetFromJsonAsync<PagedResult<MemberView>>("/api/members?q=0812345678"))!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var member = Assert.Single(found.Items);
        Assert.Equal((somying, "สมหญิง ใจงาม", 1500m), (member.Id, member.Name, member.AccumulatedAmount));
    }

    [Fact]
    public async Task A_new_phone_with_dashes_is_stored_whole_and_the_name_trimmed()
    {
        var somying = SeedMember("สมหญิง ใจดี", "0812345678");
        var somchai = await Somchai();

        await Edit(somchai, somying, "  สมหญิง ใจดี ", "089-876 5432");

        Assert.Equal(("สมหญิง ใจดี", "0898765432"), (Stored(somying).Name, Stored(somying).Phone));
    }

    // ── AC-talad-008 — BR-talad-002@v1 ──────────────────────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-008")]
    public async Task An_eleven_digit_phone_is_refused_and_the_phone_stays()
    {
        var somying = SeedMember("สมหญิง ใจดี", "0812345678");
        var somchai = await Somchai();

        var response = await Edit(somchai, somying, "สมหญิง ใจดี", "08123456789");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([("phone", "เบอร์โทรต้องเป็นตัวเลข 10 หลัก ขึ้นต้นด้วย 0")], await Errors(response));
        Assert.Equal("0812345678", Stored(somying).Phone);
    }

    [Fact]
    public async Task An_empty_name_and_a_bad_phone_are_both_answered_and_nothing_changes()
    {
        var somying = SeedMember("สมหญิง ใจดี", "0812345678");
        var somchai = await Somchai();

        var response = await Edit(somchai, somying, " ", "๐๘๑๒๓๔๕๖๗๘");

        Assert.Equal([("name", "กรุณากรอกชื่อ"), ("phone", "เบอร์โทรต้องเป็นตัวเลข 10 หลัก ขึ้นต้นด้วย 0")], await Errors(response));
        Assert.Equal(("สมหญิง ใจดี", "0812345678"), (Stored(somying).Name, Stored(somying).Phone));
    }

    // ── AC-talad-010 — BR-talad-030@v1 ──────────────────────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-010")]
    public async Task Manas_phone_cannot_become_somyings_and_stays()
    {
        SeedMember("สมหญิง ใจดี", "0812345678");
        var mana = SeedMember("มานะ ขยัน", "0898765432");
        var somchai = await Somchai();

        var response = await Edit(somchai, mana, "มานะ ขยัน", "0812345678");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal([("phone", "เบอร์โทรนี้เป็นสมาชิกอยู่แล้ว")], await Errors(response));
        Assert.Equal("0898765432", Stored(mana).Phone);
    }

    [Fact]
    public async Task A_hidden_members_phone_is_free_to_move_to(/* BR-talad-040@v2 */)
    {
        SeedMember("สมหญิง ใจดี", "0812345678", hidden: true);
        var mana = SeedMember("มานะ ขยัน", "0898765432");
        var somchai = await Somchai();

        var response = await Edit(somchai, mana, "มานะ ขยัน", "0812345678");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("0812345678", Stored(mana).Phone);
    }

    // ── NFR-talad-005 ───────────────────────────────────────────────────────────────────────────────

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Without_a_token_open_and_edit_answer_401_and_nothing_changes()
    {
        var somying = SeedMember("สมหญิง ใจดี", "0812345678");
        var anonymous = _factory.CreateClient();

        var open = await anonymous.GetAsync($"/api/members/{somying}");
        var edit = await Edit(anonymous, somying, "ใครก็ได้", "0899999999");

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (open.StatusCode, edit.StatusCode));
        Assert.Equal("สมหญิง ใจดี", Stored(somying).Name);
    }
}
