using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Talad.Api.Members;
using Talad.Application.Settings;
using Talad.Domain.Accounts;
using Talad.Domain.Members;
using Talad.Domain.Settings;

namespace Talad.Api.Settings;

public sealed record SetMemberDiscountRequest(decimal? RatePercent);

public static class MemberDiscountEndpoints
{
    public static IEndpointRouteBuilder MapMemberDiscountEndpoints(this IEndpointRouteBuilder app)
    {
        // UI-talad-017 is the owner's screen (screens.json roles · ACL-025): both calls are owner only
        var settings = app.MapGroup("/api/settings/member-discount")
            .RequireAuthorization(p => p.RequireRole(nameof(UserRole.Owner)));

        // API-030 · GET — the % in force, who set it and when; never set = 0%
        settings.MapGet("", (MemberDiscountSettings discount, CancellationToken ct) => discount.GetAsync(ct));

        // API-031 · POST — a new version (BR-talad-036@v1); a refused % leaves the one in force (state "error")
        settings.MapPost("", async (SetMemberDiscountRequest body, ClaimsPrincipal user, MemberDiscountSettings discount, CancellationToken ct) =>
        {
            try
            {
                var callerId = int.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
                return Results.Created("/api/settings/member-discount", await discount.SetAsync(body.RatePercent, callerId, ct));
            }
            catch (MemberDiscountRateException e)
            {
                return Results.BadRequest(new MemberError("RATE_OUT_OF_RANGE", [new MemberFieldError(MemberDiscountVersion.Field, e.Message)]));
            }
            catch (MemberDiscountOwnerOnlyException)
            {
                return Results.Forbid();
            }
        });

        return app;
    }
}
