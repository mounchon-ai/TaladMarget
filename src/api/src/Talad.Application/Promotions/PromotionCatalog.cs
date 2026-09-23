using System.Globalization;
using Talad.Application.Auth;
using Talad.Application.Catalog;
using Talad.Domain.Promotions;

namespace Talad.Application.Promotions;

public interface IPromotionRepository
{
    /// <summary>ACTIVE promotions with a version in force, whose current name contains the term; ordered by name.</summary>
    Task<(IReadOnlyList<Promotion> Items, int Total)> SearchActiveAsync(string? term, int page, int pageSize, CancellationToken ct);

    /// <summary>The promotion with its version in force and that version's products, whatever its status.</summary>
    Task<Promotion?> FindAsync(int id, CancellationToken ct);

    void Add(Promotion promotion);
    void Add(PromotionVersion version);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed record ProductRef(int Id, string Name);

/// <summary>A promotion with the conditions in force, as UI-talad-015 lists them and UI-talad-016 edits them.</summary>
public sealed record PromotionView(
    int Id, string Status, int VersionId, string Name, string Type,
    ProductRef? ProductA, int? QtyA, ProductRef? ProductB, int? QtyB, ProductRef? FreeProduct, int? FreeQty,
    int? RatePercent, decimal? MinSubtotal, string StartDate, string? EndDate, DateTimeOffset ChangedAt)
{
    public static PromotionView Of(Promotion p)
    {
        var v = p.CurrentVersion ?? throw new InvalidOperationException($"promotion {p.Id} has no version loaded");
        static ProductRef? Ref(int? id, Domain.Catalog.Product? product) => id is { } i ? new ProductRef(i, product?.Name ?? "") : null;
        return new PromotionView(
            p.Id, p.Status.ToString(), v.Id, v.Name, v.Type.Code(),
            Ref(v.ProductAId, v.ProductA), v.QtyA, Ref(v.ProductBId, v.ProductB), v.QtyB, Ref(v.FreeProductId, v.FreeProduct), v.FreeQty,
            v.RatePercent, v.MinSubtotal, v.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), v.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), v.CreatedAt);
    }
}

public sealed class PromotionNotFoundException(int id) : Exception($"promotion {id} does not exist or is discontinued");

/// <summary>
/// UC-talad-021 · API-025..028 — the owner's promotions. Creating makes the promotion and its first version;
/// editing adds a version and points at it, leaving the old one for the bills that used it (BR-talad-036@v1).
/// No discount is worked out here — that is the cart's, at checkout (UC-talad-004).
/// </summary>
public sealed class PromotionCatalog(IPromotionRepository promotions, IProductRepository products, IUserAccountRepository accounts, TimeProvider clock)
{
    public const int PageSize = 20; // NFR-talad-006 — 20 rows a page, paged at the server

    /// <summary>API-025</summary>
    public async Task<PagedResult<PromotionView>> SearchAsync(string? term, int page, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var (items, total) = await promotions.SearchActiveAsync(term, page, PageSize, ct);
        return new PagedResult<PromotionView>(items.Select(PromotionView.Of).ToList(), page, PageSize, total);
    }

    /// <summary>API-026</summary>
    public async Task<PromotionView> GetAsync(int id, CancellationToken ct = default) => PromotionView.Of(await ActiveAsync(id, ct));

    /// <summary>API-027 — the promotion, then its first version, then the pointer (the two rows refer to each other).</summary>
    public async Task<PromotionView> CreateAsync(PromotionTerms terms, int callerId, CancellationToken ct = default)
    {
        var owner = await accounts.FindByIdAsync(callerId, ct) ?? throw new PromotionOwnerOnlyException();
        var conditions = await CheckedAsync(terms, ct);
        var now = clock.GetUtcNow();
        var promotion = Promotion.Open(owner, now);
        promotions.Add(promotion);
        await promotions.SaveChangesAsync(ct);
        await PutInForceAsync(promotion, promotion.Draft(conditions, owner, now), ct);
        return PromotionView.Of((await promotions.FindAsync(promotion.Id, ct))!);
    }

    /// <summary>API-028 — a new version in force; the one before is left exactly as it was.</summary>
    public async Task<PromotionView> ReviseAsync(int id, PromotionTerms terms, int callerId, CancellationToken ct = default)
    {
        var owner = await accounts.FindByIdAsync(callerId, ct) ?? throw new PromotionOwnerOnlyException();
        var promotion = await ActiveAsync(id, ct);
        var conditions = await CheckedAsync(terms, ct);
        await PutInForceAsync(promotion, promotion.Draft(conditions, owner, clock.GetUtcNow()), ct);
        return PromotionView.Of((await promotions.FindAsync(promotion.Id, ct))!);
    }

    /// <summary>
    /// API-029 — ACTIVE → DISCONTINUED. From then on the list leaves it out and the api answers it as not
    /// found; its versions are not touched, so the bills that used it keep their discount (AC-talad-064).
    /// </summary>
    public async Task DiscontinueAsync(int id, int callerId, CancellationToken ct = default)
    {
        var owner = await accounts.FindByIdAsync(callerId, ct) ?? throw new PromotionOwnerOnlyException();
        var promotion = await promotions.FindAsync(id, ct) ?? throw new PromotionNotFoundException(id);
        promotion.Discontinue(owner);
        await promotions.SaveChangesAsync(ct);
    }

    private async Task PutInForceAsync(Promotion promotion, PromotionVersion version, CancellationToken ct)
    {
        promotions.Add(version);
        await promotions.SaveChangesAsync(ct);
        promotion.PointAt(version);
        await promotions.SaveChangesAsync(ct);
    }

    /// <summary>The domain's own rules, then the catalog's: every product named must exist and still be sold.</summary>
    private async Task<PromotionConditions> CheckedAsync(PromotionTerms terms, CancellationToken ct)
    {
        var conditions = PromotionConditions.Check(terms);
        var errors = new List<PromotionFieldError>();
        foreach (var (field, productId) in conditions.Products())
        {
            var product = await products.FindAsync(productId, ct);
            if (product is not { IsActive: true }) errors.Add(new(field, PromotionMessages.ProductNotSellable));
        }
        if (errors.Count > 0) throw new PromotionInvalidException(errors);
        return conditions;
    }

    private async Task<Promotion> ActiveAsync(int id, CancellationToken ct)
    {
        var promotion = await promotions.FindAsync(id, ct);
        return promotion is { Status: PromotionStatus.Active, CurrentVersion: not null } ? promotion : throw new PromotionNotFoundException(id);
    }
}
