using Talad.Domain.Accounts;

namespace Talad.Application.Auth;

public interface IUserAccountRepository
{
    Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken ct);
}

public interface IPasswordVerifier
{
    bool Verify(UserAccount account, string password);
}

public interface IAccessTokenIssuer
{
    AccessToken Issue(UserAccount account);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public enum LoginFailure
{
    UserNotFound,    // AC-talad-035 "ไม่พบชื่อผู้ใช้"
    WrongPassword,   // AC-talad-034 "รหัสผ่านไม่ถูกต้อง"
    AccountDisabled, // UC-talad-011 exception flow — message still pending at req
}

public sealed record LoginResult(AccessToken? Token, UserAccount? Account, LoginFailure? Failure)
{
    public bool Succeeded => Token is not null;
    public static LoginResult Success(AccessToken token, UserAccount account) => new(token, account, null);
    public static LoginResult Fail(LoginFailure failure) => new(null, null, failure);
}

/// <summary>
/// FUN-talad-009 · BR-talad-005@v1 — a failed sign-in says separately whether the username is
/// unknown or the password is wrong.
/// </summary>
public sealed class LoginService(IUserAccountRepository accounts, IPasswordVerifier passwords, IAccessTokenIssuer tokens)
{
    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var account = await accounts.FindByUsernameAsync(username, ct);
        if (account is null) return LoginResult.Fail(LoginFailure.UserNotFound);
        if (!passwords.Verify(account, password)) return LoginResult.Fail(LoginFailure.WrongPassword);
        if (!account.CanSignIn) return LoginResult.Fail(LoginFailure.AccountDisabled);
        return LoginResult.Success(tokens.Issue(account), account);
    }
}
