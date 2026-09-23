using Talad.Application.Auth;
using Talad.Domain.Settings;

namespace Talad.Application.Settings;

public interface IMemberDiscountRepository
{
    /// <summary>The version in force — the newest — with the name of who set it, or null if none was ever set.</summary>
    Task<(MemberDiscountVersion Version, string ChangedByName)?> LatestAsync(CancellationToken ct);
    void Add(MemberDiscountVersion version);
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>API-030 · what is in force now; never set = 0% with nobody and no time (UI-talad-017 state "empty").</summary>
public sealed record MemberDiscountView(int RatePercent, int? ChangedById, string? ChangedByName, DateTimeOffset? ChangedAt);

/// <summary>UC-talad-023 · API-030 · API-031 — the shop's one member-discount %, versioned (BR-talad-036@v1).</summary>
public sealed class MemberDiscountSettings(IMemberDiscountRepository versions, IUserAccountRepository accounts, TimeProvider clock)
{
    public async Task<MemberDiscountView> GetAsync(CancellationToken ct = default) =>
        await versions.LatestAsync(ct) is var (v, name)
            ? new MemberDiscountView(v.RatePercent, v.ChangedById, name, v.ChangedAt)
            : new MemberDiscountView(0, null, null, null);

    /// <summary>API-031 — a new version; the old ones stay for the bills that point at them.</summary>
    public async Task<MemberDiscountView> SetAsync(decimal? ratePercent, int callerId, CancellationToken ct = default)
    {
        var caller = await accounts.FindByIdAsync(callerId, ct) ?? throw new MemberDiscountOwnerOnlyException();
        var version = MemberDiscountVersion.Set(ratePercent, caller, clock.GetUtcNow());
        versions.Add(version);
        await versions.SaveChangesAsync(ct);
        return new MemberDiscountView(version.RatePercent, caller.Id, caller.DisplayName, version.ChangedAt);
    }
}
