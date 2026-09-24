using Talad.Application;
using Microsoft.AspNetCore.Mvc;
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

/// <summary>API-010 body — the cart the screen showed, and the promotion picked for each tied round, in order.</summary>
public sealed record CheckoutRequest(int? CartId, int[]? Choice);

/// <summary>
/// What a refused checkout answers. `choices` is PROMOTION_CHOICE_NEEDED's alone: the tied promotions UI-talad-003
/// lists, each with the baht it gives (BR-talad-029@v1).
/// </summary>
public sealed record CheckoutError(string Code, string Message, IReadOnlyList<PromotionChoice>? Choices = null);

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

        // API-009 · GET /api/cart/pricing?choice=&choice= — the caller's own cart priced now (UC-talad-004); each choice is the
        // promotion id the staff picked for a tied round, in order (BR-talad-029@v1)
        cart.MapGet("/pricing", ([FromQuery] int[]? choice, ClaimsPrincipal user, CartPricing pricing, CancellationToken ct) =>
            pricing.PriceAsync(OwnerId(user), choice ?? [], ct));

        // API-010 · POST /api/cart/checkout — pay the caller's own cart (ACL-003): a bill, the stock it takes, the member's
        // accumulated amount and the cart PAID in one save; the answer is the receipt, or the reason it cannot be paid
        cart.MapPost("/checkout", async (CheckoutRequest body, ClaimsPrincipal user, Checkout checkout, CancellationToken ct) =>
        {
            try
            {
                var sale = await checkout.PayAsync(OwnerId(user), body.CartId, body.Choice ?? [], ct);
                return Results.Created($"/api/sales/{sale.Id}", sale);
            }
            catch (CartNotFoundException)
            {
                return Results.NotFound(new CheckoutError("CART_NOT_FOUND", "ไม่พบตะกร้านี้")); // BR-talad-020@v1 — someone else's is not there
            }
            catch (PromotionChoiceNeededException e)
            {
                return Results.Conflict(new CheckoutError("PROMOTION_CHOICE_NEEDED", "มีโปรโมชั่นที่ลดเท่ากัน กรุณาเลือกโปรโมชั่น", e.Choices)); // AC-talad-127
            }
            catch (CartRuleException e)
            {
                return Results.Conflict(new CheckoutError(e switch
                {
                    CartNotOpenException => "CART_NOT_OPEN", // BR-talad-039@v1
                    CartEmptyException => "CART_EMPTY",
                    ProductDiscontinuedException => "PRODUCT_DISCONTINUED", // BR-talad-037@v1
                    InsufficientStockException => "INSUFFICIENT_STOCK", // BR-talad-007@v1
                    MemberNotActiveAtCheckoutException => "MEMBER_NOT_ACTIVE", // ENT-009.member
                    _ => "CART_RULE",
                }, e.Message));
            }
            catch (ConcurrentUpdateException)
            {
                // lost every retry to other sales on the same stock — nothing was saved (UI-talad-002 state "error")
                return Results.Conflict(new CheckoutError("TRY_AGAIN", "ทำรายการไม่สำเร็จ กรุณาลองใหม่"));
            }
        });

        // API-011 · GET /api/sales/{id} — a bill the caller sold, as its receipt shows it (UC-talad-005 · ACL-005); anyone
        // else's is not found (BR-talad-020@v1). What API-010's Location points at.
        app.MapGet("/api/sales/{id:int}", async (int id, ClaimsPrincipal user, SaleReceipts receipts, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await receipts.GetOwnAsync(id, OwnerId(user), ct));
            }
            catch (SaleNotFoundException)
            {
                return Results.NotFound(new CartError("SALE_NOT_FOUND", "ไม่พบบิลนี้"));
            }
        });

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
