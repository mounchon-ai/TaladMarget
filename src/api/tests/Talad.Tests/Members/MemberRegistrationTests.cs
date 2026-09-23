using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Api.Members;
using Talad.Application.Members;
using Talad.Domain.Accounts;
using Talad.Domain.Members;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Members;

/// <summary>
/// UC-talad-007 · API-013 — registering a member through the real endpoint. The ACs reuse the same phone
/// numbers (0812345678 · 0861112222) with different people on them, so every test gets its own database:
/// xUnit builds a new instance per test, and the factory is made here rather than shared by a fixture.
/// </summary>
[Trait("feature", "FE-talad-009")]
public sealed class MemberRegistrationTests : IDisposable
{
    private readonly TaladApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    // ── fixtures ────────────────────────────────────────────────────────────────────────────────────

    private async Task<HttpClient> SignedInAs(string username, string displayName, UserRole role = UserRole.Cashier)
    {
        _factory.Seed(username, displayName, "Pass#2569", role);
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "Pass#2569"));
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<HttpClient> Somchai() => SignedInAs("somchai", "สมชาย");

    private static Task<HttpResponseMessage> Register(HttpClient client, string? name, string? phone) =>
        client.PostAsJsonAsync("/api/members", new RegisterMemberRequest(name, phone));

    /// <summary>A member already on file — ACTIVE, or hidden by the owner (BR-talad-040@v2) with what they had bought.</summary>
    private int SeedMember(string name, string phone, bool hidden = false, decimal accumulated = 0m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        var owner = db.UserAccounts.FirstOrDefault(a => a.Role == UserRole.Owner);
        if (owner is null)
        {
            owner = new UserAccount($"owner-{Guid.NewGuid():N}", "เจ้าของร้าน", UserRole.Owner, DateTimeOffset.UnixEpoch);
            owner.SetPasswordHash("unused");
            db.UserAccounts.Add(owner);
            db.SaveChanges();
        }
        var member = Member.Register(name, phone, owner.Id, DateTimeOffset.UnixEpoch);
        db.Members.Add(member);
        // hiding and buying are other units' (FE-talad-015 · FE-talad-033) — set the columns as they would leave them
        db.Entry(member).Property(nameof(Member.AccumulatedAmount)).CurrentValue = accumulated;
        if (hidden)
        {
            db.Entry(member).Property(nameof(Member.Status)).CurrentValue = MemberStatus.Hidden;
            db.Entry(member).Property(nameof(Member.HiddenById)).CurrentValue = (int?)owner.Id;
            db.Entry(member).Property(nameof(Member.HiddenAt)).CurrentValue = (DateTimeOffset?)DateTimeOffset.UnixEpoch;
        }
        db.SaveChanges();
        return member.Id;
    }

    private List<Member> MembersWithPhone(string phone)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TaladDbContext>().Members.Where(m => m.Phone == phone).OrderBy(m => m.Id).ToList();
    }

    /// <summary>What the sale screen's member search would find — ACTIVE members only (API-012 is FE-talad-011's).</summary>
    private List<Member> ActiveWithPhone(string phone) => MembersWithPhone(phone).Where(m => m.Status == MemberStatus.Active).ToList();

    private int AccountId(string username)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TaladDbContext>().UserAccounts.Single(a => a.Username == username).Id;
    }

    private static async Task<(string Field, string Message)[]> Errors(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<MemberError>())!.Errors.Select(e => (e.Field, e.Message)).ToArray();

    // ── AC-talad-005 · 040 — registered ─────────────────────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-005")]
    public async Task Somchai_registers_somying_at_0812345678()
    {
        var somchai = await Somchai();

        var response = await Register(somchai, "สมหญิง ใจดี", "0812345678");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<MemberView>())!;
        Assert.Equal(("สมหญิง ใจดี", "0812345678", 0m, "Active"), (body.Name, body.Phone, body.AccumulatedAmount, body.Status));
        var found = Assert.Single(ActiveWithPhone("0812345678"));
        Assert.Equal(("สมหญิง ใจดี", body.Id, AccountId("somchai")), (found.Name, found.Id, found.CreatedById));
    }

    [Fact, Trait("ac", "AC-talad-040")]
    public async Task The_owner_registers_preecha_at_0861112222()
    {
        var owner = await SignedInAs("owner", "เจ้าของร้าน", UserRole.Owner);

        var response = await Register(owner, "ปรีชา ดีงาม", "0861112222");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var found = Assert.Single(ActiveWithPhone("0861112222"));
        Assert.Equal(("ปรีชา ดีงาม", AccountId("owner")), (found.Name, found.CreatedById));
    }

    // ── AC-talad-006 · 007 — BR-talad-002@v1 ────────────────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-006")]
    public async Task No_name_is_refused_under_the_name_field()
    {
        var somchai = await Somchai();

        var response = await Register(somchai, "", "0898765432");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([("name", "กรุณากรอกชื่อ")], await Errors(response));
        Assert.Empty(MembersWithPhone("0898765432"));
    }

    [Fact, Trait("ac", "AC-talad-007")]
    public async Task A_nine_digit_phone_is_refused_under_the_phone_field()
    {
        var somchai = await Somchai();

        var response = await Register(somchai, "มานะ ขยัน", "081234567");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([("phone", "เบอร์โทรต้องเป็นตัวเลข 10 หลัก ขึ้นต้นด้วย 0")], await Errors(response));
        Assert.Empty(MembersWithPhone("081234567"));
    }

    [Fact]
    public async Task Both_fields_wrong_answers_both_not_just_the_first()
    {
        var somchai = await Somchai();

        var response = await Register(somchai, "   ", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([("name", "กรุณากรอกชื่อ"), ("phone", "เบอร์โทรต้องเป็นตัวเลข 10 หลัก ขึ้นต้นด้วย 0")], await Errors(response));
    }

    [Theory]
    [InlineData("1812345678")]  // does not start with 0
    [InlineData("08123456789")] // 11 digits
    [InlineData("08l2345678")]  // a letter
    [InlineData("๐๘๑๒๓๔๕๖๗๘")]  // Thai digits — \d would take them
    public async Task A_phone_that_is_not_ten_ascii_digits_from_0_is_refused(string phone)
    {
        var somchai = await Somchai();

        var response = await Register(somchai, "มานะ ขยัน", phone);

        Assert.Equal([("phone", "เบอร์โทรต้องเป็นตัวเลข 10 หลัก ขึ้นต้นด้วย 0")], await Errors(response));
    }

    [Fact]
    public async Task Dashes_and_spaces_come_out_and_the_name_is_trimmed_before_storing()
    {
        var somchai = await Somchai();

        var response = await Register(somchai, "  สมหญิง ใจดี ", "081-234 5678");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var found = Assert.Single(ActiveWithPhone("0812345678"));
        Assert.Equal("สมหญิง ใจดี", found.Name);
    }

    // ── AC-talad-009 — BR-talad-030@v1 ──────────────────────────────────────────────────────────────

    [Fact, Trait("ac", "AC-talad-009")]
    public async Task A_phone_an_active_member_holds_is_refused()
    {
        SeedMember("สมหญิง ใจดี", "0812345678");
        var somchai = await Somchai();

        var response = await Register(somchai, "สมศรี มีสุข", "0812345678");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal([("phone", "เบอร์โทรนี้เป็นสมาชิกอยู่แล้ว")], await Errors(response));
        Assert.Equal(["สมหญิง ใจดี"], ActiveWithPhone("0812345678").Select(m => m.Name));
    }

    [Fact]
    public async Task Pressing_save_twice_makes_one_member()
    {
        var somchai = await Somchai();

        var first = await Register(somchai, "สมหญิง ใจดี", "0812345678");
        var second = await Register(somchai, "สมหญิง ใจดี", "0812345678");

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Conflict), (first.StatusCode, second.StatusCode));
        Assert.Single(MembersWithPhone("0812345678"));
    }

    // ── AC-talad-011 · 090 — BR-talad-040@v2: a hidden member's phone is free ───────────────────────

    [Fact, Trait("ac", "AC-talad-011")]
    public async Task The_phone_of_hidden_somying_goes_to_a_new_member_at_0()
    {
        var somying = SeedMember("สมหญิง ใจดี", "0812345678", hidden: true, accumulated: 1500m);
        var somchai = await Somchai();

        var response = await Register(somchai, "สมศรี มีสุข", "0812345678");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var found = Assert.Single(ActiveWithPhone("0812345678"));
        Assert.Equal(("สมศรี มีสุข", 0m), (found.Name, found.AccumulatedAmount));
        Assert.NotEqual(somying, found.Id);
        // the hidden member stays as the owner left her — not brought back, not emptied
        var hidden = MembersWithPhone("0812345678").Single(m => m.Id == somying);
        Assert.Equal((MemberStatus.Hidden, 1500m, "สมหญิง ใจดี"), (hidden.Status, hidden.AccumulatedAmount, hidden.Name));
    }

    [Fact, Trait("ac", "AC-talad-090")]
    public async Task Hidden_preecha_who_never_bought_can_be_registered_again_as_a_new_member()
    {
        var before = SeedMember("ปรีชา ดีงาม", "0861112222", hidden: true);
        var somchai = await Somchai();

        var response = await Register(somchai, "ปรีชา ดีงาม", "0861112222");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var found = Assert.Single(ActiveWithPhone("0861112222"));
        Assert.Equal(("ปรีชา ดีงาม", 0m), (found.Name, found.AccumulatedAmount));
        Assert.NotEqual(before, found.Id);
        Assert.Equal(2, MembersWithPhone("0861112222").Count);
    }

    // ── NFR-talad-005 ───────────────────────────────────────────────────────────────────────────────

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Without_a_token_registering_answers_401_and_makes_nobody()
    {
        var response = await Register(_factory.CreateClient(), "สมหญิง ใจดี", "0812345678");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(MembersWithPhone("0812345678"));
    }
}
