using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Talad.Application.Catalog;
using Talad.Application.Sales;
using Talad.Domain.Sales;
using Talad.Infrastructure.Storage;

namespace Talad.Api.Sales;

public sealed record AddLineRequest(int ProductId);

public sealed record SetQtyRequest(int Qty);

/// <summary>API-008 — the member to bind, or null to unbind.</summary>
public sealed record SetMemberRequest(int? MemberId);

/// <summary>What a refused cart change answers — `message` is the sentence the person reads.</summary>
public sealed record CartError(string Code, string Message);

public static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCatalogAndCartEndpoints(this IEndpointRouteBuilder app)
    {
        // API-003 · GET /api/products?search=&page= — ACTIVE products, 20 a page, current price and low-stock flag
        app.MapGet("/api/products", (string? search, int? page, ProductCatalog catalog, CancellationToken ct) =>
            catalog.SearchAsync(search, page ?? 1, ct));

        // API-043 · GET /api/products/{id}/image — the file on the server's disk (DEC-002)
        app.MapGet("/api/products/{id:int}/image", async (int id, IProductRepository products, ProductImageFiles images, CancellationToken ct) =>
        {
            var product = await products.FindAsync(id, ct);
            var file = images.Resolve(product?.ImagePath);
            return file is { } f ? Results.File(f.FullPath, f.ContentType) : Results.NotFound();
        });

        var cart = app.MapGroup("/api/cart");

        // API-004 · GET /api/cart — the caller's OPEN cart, opened now if there is none
        cart.MapGet("", (ClaimsPrincipal user, CartService carts, CancellationToken ct) =>
            carts.GetAsync(OwnerId(user), ct));

        // API-005 · POST /api/cart/lines — one more of a product
        cart.MapPost("/lines", (AddLineRequest body, ClaimsPrincipal user, CartService carts, CancellationToken ct) =>
            Guard(() => carts.AddOneAsync(OwnerId(user), body.ProductId, ct)));

        // API-006 · PATCH /api/cart/lines/{productId} — set the quantity; 0 takes the line out
        cart.MapPatch("/lines/{productId:int}", (int productId, SetQtyRequest body, ClaimsPrincipal user, CartService carts, CancellationToken ct) =>
            Guard(() => carts.SetQtyAsync(OwnerId(user), productId, body.Qty, ct)));

        // API-007 · DELETE /api/cart/lines/{productId}
        cart.MapDelete("/lines/{productId:int}", (int productId, ClaimsPrincipal user, CartService carts, CancellationToken ct) =>
            Guard(() => carts.RemoveAsync(OwnerId(user), productId, ct)));

        // API-008 · PUT /api/cart/member — bind an ACTIVE member to the caller's own cart, or unbind
        cart.MapPut("/member", (SetMemberRequest body, ClaimsPrincipal user, CartService carts, CancellationToken ct) =>
            Guard(() => carts.SetMemberAsync(OwnerId(user), body.MemberId, ct)));

        return app;
    }

    /// <summary>The cart is always the caller's own — taken from the token, never from the request (BR-talad-020@v1).</summary>
    private static int OwnerId(ClaimsPrincipal user) => int.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static async Task<IResult> Guard(Func<Task<CartView>> change)
    {
        try
        {
            return Results.Ok(await change());
        }
        catch (InsufficientStockException e)
        {
            return Results.Conflict(new CartError("INSUFFICIENT_STOCK", e.Message)); // BR-talad-007@v1
        }
        catch (ProductDiscontinuedException e)
        {
            return Results.Conflict(new CartError("PRODUCT_DISCONTINUED", e.Message)); // BR-talad-037@v1
        }
        catch (CartNotOpenException e)
        {
            return Results.Conflict(new CartError("CART_NOT_OPEN", e.Message)); // BR-talad-001@v1
        }
        catch (ProductNotFoundException e)
        {
            return Results.NotFound(new CartError("PRODUCT_NOT_FOUND", e.Message));
        }
        catch (CartLineNotFoundException e)
        {
            return Results.NotFound(new CartError("LINE_NOT_FOUND", e.Message));
        }
        catch (MemberNotBindableException e)
        {
            return Results.NotFound(new CartError("MEMBER_NOT_FOUND", e.Message)); // BR-talad-040@v2
        }
    }
}
