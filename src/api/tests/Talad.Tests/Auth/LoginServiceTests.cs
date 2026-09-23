using Talad.Application.Auth;
using Talad.Domain.Accounts;
using Talad.Infrastructure.Auth;

namespace Talad.Tests.Auth;

[Trait("feature", "FE-talad-001")]
public class LoginServiceTests
{
    private static readonly IdentityPasswordHasher Hasher = new();

    private static UserAccount Somchai(AccountStatus status = AccountStatus.Active)
    {
        var a = new UserAccount("somchai", "สมชาย", UserRole.Cashier, DateTimeOffset.UnixEpoch);
        a.SetPasswordHash(Hasher.Hash(a, "Somchai#2569"));
        if (status == AccountStatus.Disabled) typeof(UserAccount).GetProperty(nameof(UserAccount.Status))!.SetValue(a, AccountStatus.Disabled);
        return a;
    }

    private static LoginService Service(params UserAccount[] accounts) =>
        new(new FakeAccounts(accounts), Hasher, new FakeTokens());

    [Fact, Trait("ac", "AC-talad-033")]
    public async Task Correct_username_and_password_signs_in_as_that_user()
    {
        var result = await Service(Somchai()).LoginAsync("somchai", "Somchai#2569");

        Assert.True(result.Succeeded);
        Assert.Equal("somchai", result.Account!.Username);
        Assert.Equal("token-for-somchai", result.Token!.Value);
    }

    [Fact, Trait("ac", "AC-talad-034")]
    public async Task Wrong_password_is_reported_as_wrong_password()
    {
        var result = await Service(Somchai()).LoginAsync("somchai", "somchai2569");

        Assert.False(result.Succeeded);
        Assert.Equal(LoginFailure.WrongPassword, result.Failure);
    }

    [Fact, Trait("ac", "AC-talad-035")]
    public async Task Unknown_username_is_reported_as_user_not_found()
    {
        var result = await Service(Somchai()).LoginAsync("somchay", "anything");

        Assert.False(result.Succeeded);
        Assert.Equal(LoginFailure.UserNotFound, result.Failure);
    }

    [Fact]
    public async Task Disabled_account_cannot_sign_in()
    {
        var result = await Service(Somchai(AccountStatus.Disabled)).LoginAsync("somchai", "Somchai#2569");

        Assert.False(result.Succeeded);
        Assert.Equal(LoginFailure.AccountDisabled, result.Failure);
    }

    private sealed class FakeAccounts(UserAccount[] accounts) : IUserAccountRepository
    {
        public Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken ct) =>
            Task.FromResult(accounts.SingleOrDefault(a => a.Username == username));

        public Task<UserAccount?> FindByIdAsync(int id, CancellationToken ct) =>
            Task.FromResult(accounts.SingleOrDefault(a => a.Id == id));
    }

    private sealed class FakeTokens : IAccessTokenIssuer
    {
        public AccessToken Issue(UserAccount account) => new($"token-for-{account.Username}", DateTimeOffset.MaxValue);
    }
}
