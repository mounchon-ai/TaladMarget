using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Talad.Application.Catalog;
using Talad.Application.Sales;
using Talad.Domain.Accounts;
using Talad.Domain.Catalog;

namespace Talad.Api.Catalog;

/// <summary>API-022 body — loose, so a wrong value comes back under its own field rather than as the framework's JSON error.</summary>
public sealed record RepriceRequest(decimal? Price, string? Source);

/// <summary>
/// API-024 body — loose for the same reason. <c>Quantity</c> is RECEIVE's and SPOILED's (+/−), <c>CountedQty</c> is
/// RECOUNT's; <c>RequestKey</c> is the key the form minted when it opened (BR-talad-041@v1).
/// </summary>
public sealed record StockAdjustmentRequest(string? Reason, decimal? Quantity, decimal? CountedQty, string? Note, string? RequestKey);

public sealed record ProductFieldError(string Field, string Message);

/// <summary>
/// What a refusal answers: PRICE_INVALID · STOCK_ADJUSTMENT_INVALID (400, under the field) · STOCK_ADJUSTMENT_DUPLICATE
/// (409) · PRODUCT_NOT_FOUND (404).
/// </summary>
public sealed record ProductError(string Code, IReadOnlyList<ProductFieldError> Errors);

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        // UI-talad-012 · 013 are the owner's screens (ACL-019). API-003 and API-043 stay open to the cashier —
        // the sales screen reads them.
        var products = app.MapGroup("/api/products").RequireAuthorization(p => p.RequireRole(nameof(UserRole.Owner)));

        // API-019 · GET /api/products/{id}?historyPage=&adjustmentsPage= — the product, its price history and its stock
        // adjustments, each 20 a page
        products.MapGet("/{id:int}", (int id, int? historyPage, int? adjustmentsPage, ProductPricing pricing, CancellationToken ct) =>
            Guard(async () => Results.Ok(await pricing.GetDetailAsync(id, historyPage ?? 1, adjustmentsPage ?? 1, ct))));

        // API-022 · POST /api/products/{id}/prices — a new price version in force from now (BR-talad-033@v1)
        products.MapPost("/{id:int}/prices", (int id, RepriceRequest body, ClaimsPrincipal user, ProductPricing pricing, CancellationToken ct) =>
            Guard(async () => Results.Created($"/api/products/{id}", await pricing.RepriceAsync(id, body.Price, body.Source, CallerId(user), ct))));

        // API-024 · POST /api/products/{id}/stock-adjustments — one hand change of stock under the form's key (BR-talad-032@v1 ·
        // BR-talad-041@v1); answers the product as API-019 draws it, the new row first
        products.MapPost("/{id:int}/stock-adjustments", (int id, StockAdjustmentRequest body, ClaimsPrincipal user, StockAdjusting stock, CancellationToken ct) =>
            Guard(async () => Results.Created($"/api/products/{id}",
                await stock.AdjustAsync(id, body.Reason, body.Quantity, body.CountedQty, body.Note, body.RequestKey, CallerId(user), ct))));

        // API-023 · POST /api/products/{id}/discontinue — ACTIVE → DISCONTINUED (ACL-020); already discontinued or
        // never there is PRODUCT_NOT_FOUND
        products.MapPost("/{id:int}/discontinue", (int id, ClaimsPrincipal user, ProductPricing pricing, CancellationToken ct) =>
            Guard(async () =>
            {
                await pricing.DiscontinueAsync(id, CallerId(user), ct);
                return Results.NoContent();
            }));

        return app;
    }

    private static int CallerId(ClaimsPrincipal user) => int.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static async Task<IResult> Guard(Func<Task<IResult>> call)
    {
        try
        {
            return await call();
        }
        catch (PriceInvalidException e)
        {
            return Results.BadRequest(new ProductError("PRICE_INVALID", [new(e.Field, e.Message)]));
        }
        catch (Exception e) when (e is ProductNotFoundException or ProductNotActiveException)
        {
            return Results.NotFound(new ProductError("PRODUCT_NOT_FOUND", []));
        }
        catch (StockAdjustmentInvalidException e)
        {
            return Results.BadRequest(new ProductError("STOCK_ADJUSTMENT_INVALID", [new(e.Field, e.Message)]));
        }
        catch (StockAdjustmentDuplicateException e)
        {
            return Results.Conflict(new ProductError("STOCK_ADJUSTMENT_DUPLICATE", [new("requestKey", e.Message)]));
        }
        catch (Exception e) when (e is PriceOwnerOnlyException or ProductOwnerOnlyException or StockOwnerOnlyException)
        {
            return Results.Forbid();
        }
    }
}
