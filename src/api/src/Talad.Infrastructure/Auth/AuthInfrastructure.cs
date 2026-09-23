using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Talad.Application.Auth;
using Talad.Domain.Accounts;
using Talad.Infrastructure.Persistence;

namespace Talad.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "talad-api";
    public string Audience { get; set; } = "talad-web";
    /// <summary>HMAC-SHA256 key, at least 32 bytes. Supplied by configuration, never committed.</summary>
    public string SigningKey { get; set; } = "";
    public int LifetimeMinutes { get; set; } = 720;

    public SymmetricSecurityKey Key() => new(Encoding.UTF8.GetBytes(SigningKey));
}

internal sealed class UserAccountRepository(TaladDbContext db) : IUserAccountRepository
{
    public Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken ct) =>
        db.UserAccounts.SingleOrDefaultAsync(x => x.Username == username, ct);
}

/// <summary>ENT-008.passwordHash — .NET Identity's hasher is the only way a password is stored or checked.</summary>
public sealed class IdentityPasswordHasher : IPasswordVerifier
{
    private readonly PasswordHasher<UserAccount> _hasher = new();

    public string Hash(UserAccount account, string password) => _hasher.HashPassword(account, password);

    public bool Verify(UserAccount account, string password) =>
        _hasher.VerifyHashedPassword(account, account.PasswordHash, password) != PasswordVerificationResult.Failed;
}

internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock) : IAccessTokenIssuer
{
    public AccessToken Issue(UserAccount account)
    {
        var o = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(o.LifetimeMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, account.Username),
            new Claim("name", account.DisplayName),
            new Claim("role", account.Role.ToString()),
        };
        var token = new JwtSecurityToken(
            issuer: o.Issuer,
            audience: o.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(o.Key(), SecurityAlgorithms.HmacSha256));
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
