using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Talad.Api.Auth;
using Talad.Api.Members;
using Talad.Api.Settings;
using Talad.Application.Settings;
using Talad.Domain.Accounts;
using Talad.Domain.Settings;
using Talad.Infrastructure.Persistence;
using Talad.Tests.Auth;

namespace Talad.Tests.Settings;

/// <summary>
/// UC-talad-023 · API-030 · API-031 — the owner sets the shop's one member-discount %, kept as versions.
/// AC-talad-061's bill half (old bills keep 5%) needs sales, which do not exist yet; what is proved here
/// is the half this unit owns — a new % is a new row, and the old row stays for the bills that point at it.
/// </summary>
[Trait("feature", "FE-talad-029")]
public sealed class MemberDiscountTests : IDisposable
{
    private readonly TaladApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

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

    private static Task<HttpResponseMessage> Set(HttpClient client, decimal? rate) =>
        client.PostAsJsonAsync("/api/settings/member-discount", new SetMemberDiscountRequest(rate));

    private static async Task<MemberDiscountView> Current(HttpClient client) =>
        (await client.GetFromJsonAsync<MemberDiscountView>("/api/settings/member-discount"))!;

    private List<int> StoredRates()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TaladDbContext>().MemberDiscountVersions.OrderBy(v => v.Id).Select(v => v.RatePercent).ToList();
    }

    [Fact]
    public async Task Never_set_is_0_percent_with_nobody_and_no_time()
    {
        var owner = await Owner();

        var current = await Current(owner);

        Assert.Equal((0, (int?)null, (string?)null, (DateTimeOffset?)null), (current.RatePercent, current.ChangedById, current.ChangedByName, current.ChangedAt));
    }

    [Fact, Trait("ac", "AC-talad-061")]
    public async Task From_5_to_10_percent_the_new_one_is_in_force_and_the_5_percent_version_stays()
    {
        var owner = await Owner();
        await Set(owner, 5);

        var response = await Set(owner, 10);
        var current = await Current(owner);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((10, "เจ้าของร้าน"), (current.RatePercent, current.ChangedByName));
        Assert.NotNull(current.ChangedAt);
        Assert.Equal([5, 10], StoredRates());
    }

    [Fact]
    public async Task Zero_percent_turns_the_member_discount_off()
    {
        var owner = await Owner();
        await Set(owner, 5);

        await Set(owner, 0);

        Assert.Equal(0, (await Current(owner)).RatePercent);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(5.5)]
    public async Task A_rate_that_is_not_a_whole_0_to_100_is_refused_and_the_one_in_force_stays(double rate)
    {
        var owner = await Owner();
        await Set(owner, 5);

        var response = await Set(owner, (decimal)rate);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<MemberError>())!;
        Assert.Equal([("ratePercent", "ส่วนลดสมาชิกต้องเป็นจำนวนเต็ม 0–100")], error.Errors.Select(e => (e.Field, e.Message)));
        Assert.Equal(5, (await Current(owner)).RatePercent);
        Assert.Equal([5], StoredRates());
    }

    [Fact]
    public async Task No_rate_at_all_is_refused()
    {
        var owner = await Owner();

        var response = await Set(owner, null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(StoredRates());
    }

    [Fact]
    public async Task A_seller_can_neither_read_nor_set_it()
    {
        var somchai = await SignedInAs("somchai", "สมชาย", UserRole.Cashier);

        var read = await somchai.GetAsync("/api/settings/member-discount");
        var set = await Set(somchai, 50);

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (read.StatusCode, set.StatusCode));
        Assert.Empty(StoredRates());
    }

    [Fact]
    public void The_domain_refuses_a_seller_even_without_the_endpoint_policy()
    {
        var seller = new UserAccount("somchai", "สมชาย", UserRole.Cashier, DateTimeOffset.UnixEpoch);

        Assert.Throws<MemberDiscountOwnerOnlyException>(() => MemberDiscountVersion.Set(5, seller, DateTimeOffset.UnixEpoch));
    }

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Without_a_token_both_answer_401()
    {
        var anonymous = _factory.CreateClient();

        var read = await anonymous.GetAsync("/api/settings/member-discount");
        var set = await Set(anonymous, 5);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (read.StatusCode, set.StatusCode));
    }
}
