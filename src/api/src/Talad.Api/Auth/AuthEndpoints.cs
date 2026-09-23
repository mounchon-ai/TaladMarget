using Talad.Application.Auth;

namespace Talad.Api.Auth;

public sealed record LoginRequest(string? Username, string? Password);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, string Username, string DisplayName, string Role);

public sealed record LoginError(string Code, string? Message);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth");

        // API-001 · POST /api/auth/login — the one endpoint that needs no JWT (BR-talad-005@v1)
        auth.MapPost("/login", async (LoginRequest body, LoginService login, CancellationToken ct) =>
        {
            var result = await login.LoginAsync(body.Username ?? "", body.Password ?? "", ct);
            if (result.Succeeded)
            {
                var a = result.Account!;
                return Results.Ok(new LoginResponse(result.Token!.Value, result.Token.ExpiresAt, a.Username, a.DisplayName, a.Role.ToString()));
            }
            return Results.Json(ToError(result.Failure!.Value), statusCode: StatusCodes.Status401Unauthorized);
        }).AllowAnonymous();

        // API-002 · POST /api/auth/logout — the token is stateless, so ending the session is the
        // client dropping it; a held-over cart stays where it is (BR-talad-020@v1).
        auth.MapPost("/logout", () => Results.NoContent());

        return app;
    }

    // BR-talad-005@v1 — the two sign-in failures are told apart, word for word as AC-talad-034/035 say
    internal static LoginError ToError(LoginFailure failure) => failure switch
    {
        LoginFailure.UserNotFound => new("USER_NOT_FOUND", "ไม่พบชื่อผู้ใช้"),
        LoginFailure.WrongPassword => new("WRONG_PASSWORD", "รหัสผ่านไม่ถูกต้อง"),
        // UC-talad-011 exception flow: the wording is still open at req — no message is invented here
        LoginFailure.AccountDisabled => new("ACCOUNT_DISABLED", null),
        _ => throw new ArgumentOutOfRangeException(nameof(failure)),
    };
}
