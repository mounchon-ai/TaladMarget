using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Talad.Api.Auth;
using Talad.Domain.Accounts;
using Talad.Infrastructure.Auth;
using Talad.Infrastructure.Persistence;

namespace Talad.Tests.Auth;

public sealed class TaladApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "test-signing-key-for-talad-api-0123456789";
    private readonly string _db = $"talad-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TaladDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<TaladDbContext>));
            services.AddDbContext<TaladDbContext>(o => o.UseInMemoryDatabase(_db));
        });
    }

    public void Seed(string username, string displayName, string password, UserRole role = UserRole.Cashier)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaladDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IdentityPasswordHasher>();
        var account = new UserAccount(username, displayName, role, DateTimeOffset.UnixEpoch);
        account.SetPasswordHash(hasher.Hash(account, password));
        db.UserAccounts.Add(account);
        db.SaveChanges();
    }
}

[Trait("feature", "FE-talad-001")]
public class AuthApiTests : IClassFixture<TaladApiFactory>
{
    private readonly TaladApiFactory _factory;
    private readonly HttpClient _client;

    public AuthApiTests(TaladApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        if (!scope.ServiceProvider.GetRequiredService<TaladDbContext>().UserAccounts.Any(a => a.Username == "somchai"))
            factory.Seed("somchai", "สมชาย", "Somchai#2569");
    }

    private Task<HttpResponseMessage> Login(string username, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));

    [Fact, Trait("ac", "AC-talad-033")]
    public async Task Login_succeeds_and_the_token_names_somchai()
    {
        var response = await Login("somchai", "Somchai#2569");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal("somchai", body.Username);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        Assert.Equal("somchai", jwt.Claims.Single(c => c.Type == "unique_name").Value);
    }

    [Fact, Trait("ac", "AC-talad-034")]
    public async Task Wrong_password_answers_รหัสผ่านไม่ถูกต้อง()
    {
        var response = await Login("somchai", "somchai2569");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<LoginError>())!;
        Assert.Equal("WRONG_PASSWORD", error.Code);
        Assert.Equal("รหัสผ่านไม่ถูกต้อง", error.Message);
    }

    [Fact, Trait("ac", "AC-talad-035")]
    public async Task Unknown_user_answers_ไม่พบชื่อผู้ใช้()
    {
        var response = await Login("somchay", "whatever");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<LoginError>())!;
        Assert.Equal("USER_NOT_FOUND", error.Code);
        Assert.Equal("ไม่พบชื่อผู้ใช้", error.Message);
    }

    // AC-talad-036 (api half) · NFR-talad-005 — an endpoint other than sign-in refuses a missing,
    // expired or wrongly signed token with 401, and accepts a valid one.
    [Fact, Trait("ac", "AC-talad-036")]
    public async Task Protected_endpoint_without_a_token_answers_401()
    {
        var response = await _client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Protected_endpoint_with_an_expired_token_answers_401()
    {
        var token = Forge(TaladApiFactory.SigningKey, expires: DateTime.UtcNow.AddMinutes(-1));

        var response = await Send("/api/auth/logout", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact, Trait("nfr", "NFR-talad-005")]
    public async Task Protected_endpoint_with_a_wrongly_signed_token_answers_401()
    {
        var token = Forge("another-key-that-is-not-the-api-signing-key", expires: DateTime.UtcNow.AddMinutes(30));

        var response = await Send("/api/auth/logout", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact, Trait("ac", "AC-talad-033")]
    public async Task Logout_with_the_issued_token_answers_204()
    {
        var login = (await (await Login("somchai", "Somchai#2569")).Content.ReadFromJsonAsync<LoginResponse>())!;

        var response = await Send("/api/auth/logout", login.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private Task<HttpResponseMessage> Send(string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private static string Forge(string key, DateTime expires)
    {
        var token = new JwtSecurityToken(
            issuer: "talad-api",
            audience: "talad-web",
            claims: [new("unique_name", "somchai")],
            notBefore: expires.AddHours(-1),
            expires: expires,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
