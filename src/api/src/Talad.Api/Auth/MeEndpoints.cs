using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Talad.Application.Navigation;

namespace Talad.Api.Auth;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        // API-044 · GET /api/me — name, role, and the menu this role opens (UC-talad-012 · BR-talad-018@v1).
        // Needs a valid JWT like every endpoint but sign-in (fallback policy); the account is read again
        // so a disabled one stops here even while its token has not expired.
        app.MapGet("/api/me", async (ClaimsPrincipal user, CurrentUserService current, CancellationToken ct) =>
        {
            if (!int.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)) return Results.Unauthorized();
            var me = await current.GetAsync(id, ct);
            return me is null ? Results.Unauthorized() : Results.Ok(me);
        });
        return app;
    }
}
