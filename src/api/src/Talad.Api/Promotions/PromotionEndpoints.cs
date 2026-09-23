using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Talad.Application.Promotions;
using Talad.Domain.Accounts;
using Talad.Domain.Promotions;

namespace Talad.Api.Promotions;

/// <summary>
/// API-027 · API-028 body — the owner's input, every value loose so a wrong one comes back under its own
/// field instead of as the framework's JSON error.
/// </summary>
public sealed record PromotionRequest(
    string? Name, string? Type,
    decimal? ProductA, decimal? QtyA, decimal? ProductB, decimal? QtyB, decimal? FreeProduct, decimal? FreeQty,
    decimal? RatePercent, decimal? MinSubtotal, string? StartDate, string? EndDate)
{
    public PromotionTerms Terms() =>
        new(Name, Type, ProductA, QtyA, ProductB, QtyB, FreeProduct, FreeQty, RatePercent, MinSubtotal, StartDate, EndDate);
}

/// <summary>What a refused promotion answers: each message under the ENT-006 field it is about.</summary>
public sealed record PromotionError(string Code, IReadOnlyList<PromotionFieldError> Errors);

public static class PromotionEndpoints
{
    public static IEndpointRouteBuilder MapPromotionEndpoints(this IEndpointRouteBuilder app)
    {
        // UI-talad-015 · 016 are the owner's screens (ACL-023): every call here is owner only
        var promotions = app.MapGroup("/api/promotions").RequireAuthorization(p => p.RequireRole(nameof(UserRole.Owner)));

        // API-025 · GET /api/promotions?search=&page= — ACTIVE promotions with their conditions in force, 20 a page
        promotions.MapGet("", (string? search, int? page, PromotionCatalog catalog, CancellationToken ct) =>
            catalog.SearchAsync(search, page ?? 1, ct));

        // API-026 · GET /api/promotions/{id}
        promotions.MapGet("/{id:int}", (int id, PromotionCatalog catalog, CancellationToken ct) =>
            Guard(async () => Results.Ok(await catalog.GetAsync(id, ct))));

        // API-027 · POST /api/promotions — the promotion and its first version
        promotions.MapPost("", (PromotionRequest body, ClaimsPrincipal user, PromotionCatalog catalog, CancellationToken ct) =>
            Guard(async () =>
            {
                var created = await catalog.CreateAsync(body.Terms(), CallerId(user), ct);
                return Results.Created($"/api/promotions/{created.Id}", created);
            }));

        // API-028 · POST /api/promotions/{id}/versions — new conditions in force from now; the old version stays
        promotions.MapPost("/{id:int}/versions", (int id, PromotionRequest body, ClaimsPrincipal user, PromotionCatalog catalog, CancellationToken ct) =>
            Guard(async () => Results.Created($"/api/promotions/{id}", await catalog.ReviseAsync(id, body.Terms(), CallerId(user), ct))));

        return app;
    }

    private static int CallerId(ClaimsPrincipal user) => int.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static async Task<IResult> Guard(Func<Task<IResult>> call)
    {
        try
        {
            return await call();
        }
        catch (PromotionInvalidException e)
        {
            return Results.BadRequest(new PromotionError("PROMOTION_INVALID", e.Errors));
        }
        catch (Exception e) when (e is PromotionNotFoundException or PromotionNotActiveException)
        {
            return Results.NotFound(new PromotionError("PROMOTION_NOT_FOUND", []));
        }
        catch (PromotionOwnerOnlyException)
        {
            return Results.Forbid();
        }
    }
}
