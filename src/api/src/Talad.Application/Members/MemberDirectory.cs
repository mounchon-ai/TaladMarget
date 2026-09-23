using Talad.Application.Catalog;

namespace Talad.Application.Members;

/// <summary>
/// API-012 · UC-talad-009 — find a member to sell to. Every seller sees every ACTIVE member of the shop,
/// whoever registered them, with the whole phone and what they have bought (BR-talad-031@v1).
/// </summary>
public sealed class MemberDirectory(IMemberRepository members)
{
    public const int PageSize = 20; // NFR-talad-006 — 20 rows a page, paged at the server

    public async Task<PagedResult<MemberView>> SearchAsync(string? term, int page, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var (items, total) = await members.SearchActiveAsync(term, page, PageSize, ct);
        return new PagedResult<MemberView>(items.Select(MemberView.Of).ToList(), page, PageSize, total);
    }
}
