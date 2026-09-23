using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Talad.Application.Members;
using Talad.Domain.Accounts;
using Talad.Domain.Members;

namespace Talad.Api.Members;

public sealed record RegisterMemberRequest(string? Name, string? Phone);

public sealed record EditMemberRequest(string? Name, string? Phone);

/// <summary>
/// What a refused member change answers. Each error names the ENT-004 field it sits under, and `message`
/// is the sentence the person reads — one shape for the 400 and the 409.
/// </summary>
public sealed record MemberError(string Code, IReadOnlyList<MemberFieldError> Errors);

public static class MemberEndpoints
{
    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder app)
    {
        // API-012 · GET /api/members?q=&page= — ACTIVE members by whole phone or part of the name, 20 a page
        app.MapGet("/api/members", (string? q, int? page, MemberDirectory directory, CancellationToken ct) =>
            directory.SearchAsync(q, page ?? 1, ct));

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

        // API-014 · GET /api/members/{id} — one ACTIVE member; a hidden or unknown one is not found
        app.MapGet("/api/members/{id:int}", async (int id, MemberProfile profile, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await profile.GetAsync(id, ct));
            }
            catch (MemberNotFoundException)
            {
                return NotFound();
            }
        });

        // API-015 · PUT /api/members/{id} — a new name or phone, or why not (BR-talad-002@v1 · BR-talad-030@v1)
        app.MapPut("/api/members/{id:int}", async (int id, EditMemberRequest body, MemberProfile profile, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await profile.EditAsync(id, body.Name, body.Phone, ct));
            }
            catch (Exception e) when (e is MemberNotFoundException or MemberNotActiveException)
            {
                return NotFound();
            }
            catch (MemberInvalidException e)
            {
                return Results.BadRequest(new MemberError("MEMBER_INVALID", e.Errors));
            }
            catch (MemberPhoneTakenException e)
            {
                return Results.Conflict(new MemberError("PHONE_TAKEN", e.Errors));
            }
        });

        // API-016 · POST /api/members/{id}/hide — owner only (BR-talad-019@v1 · ACL-014): a seller is
        // refused here with 403, and again in the domain if the policy were ever bypassed
        app.MapPost("/api/members/{id:int}/hide", async (int id, ClaimsPrincipal user, MemberProfile profile, CancellationToken ct) =>
        {
            try
            {
                await profile.HideAsync(id, CallerId(user), ct);
                return Results.NoContent();
            }
            catch (Exception e) when (e is MemberNotFoundException or MemberNotActiveException)
            {
                return NotFound();
            }
            catch (OwnerOnlyException)
            {
                return Results.Forbid();
            }
        }).RequireAuthorization(p => p.RequireRole(nameof(UserRole.Owner)));

        return app;
    }

    /// <summary>A hidden or unknown member — no field is at fault, so no field error.</summary>
    private static IResult NotFound() => Results.NotFound(new MemberError("MEMBER_NOT_FOUND", []));

    /// <summary>ENT-004.createdBy is the caller — taken from the token, never from the request.</summary>
    private static int CallerId(ClaimsPrincipal user) => int.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
