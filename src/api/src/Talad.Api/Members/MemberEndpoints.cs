using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Talad.Application.Members;
using Talad.Domain.Members;

namespace Talad.Api.Members;

public sealed record RegisterMemberRequest(string? Name, string? Phone);

/// <summary>
/// What a refused member change answers. Each error names the ENT-004 field it sits under, and `message`
/// is the sentence the person reads — one shape for the 400 and the 409.
/// </summary>
public sealed record MemberError(string Code, IReadOnlyList<MemberFieldError> Errors);

public static class MemberEndpoints
{
    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder app)
    {
        // API-013 · POST /api/members — a new member at ฿0, or why not
        app.MapPost("/api/members", async (RegisterMemberRequest body, ClaimsPrincipal user, MemberRegistration registration, CancellationToken ct) =>
        {
            try
            {
                var member = await registration.RegisterAsync(body.Name, body.Phone, CallerId(user), ct);
                return Results.Created($"/api/members/{member.Id}", member);
            }
            catch (MemberInvalidException e)
            {
                return Results.BadRequest(new MemberError("MEMBER_INVALID", e.Errors)); // BR-talad-002@v1
            }
            catch (MemberPhoneTakenException e)
            {
                return Results.Conflict(new MemberError("PHONE_TAKEN", e.Errors)); // BR-talad-030@v1
            }
        });

        return app;
    }

    /// <summary>ENT-004.createdBy is the caller — taken from the token, never from the request.</summary>
    private static int CallerId(ClaimsPrincipal user) => int.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
