using Talad.Application.Auth;

namespace Talad.Application.Navigation;

public sealed record CurrentUser(string Username, string DisplayName, string Role, IReadOnlyList<MenuItem> Menu, IReadOnlyList<string> Screens);

/// <summary>API-044 · who is signed in, and what their role may open (UC-talad-012).</summary>
public sealed class CurrentUserService(IUserAccountRepository accounts)
{
    /// <returns>null when the account behind the token no longer exists or has been disabled.</returns>
    public async Task<CurrentUser?> GetAsync(int accountId, CancellationToken ct = default)
    {
        var account = await accounts.FindByIdAsync(accountId, ct);
        if (account is null || !account.CanSignIn) return null;
        return new CurrentUser(
            account.Username,
            account.DisplayName,
            account.Role.ToString(),
            ScreenAccess.MenuFor(account.Role),
            ScreenAccess.ScreensFor(account.Role));
    }
}
