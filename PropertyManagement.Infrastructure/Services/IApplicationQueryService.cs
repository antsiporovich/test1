using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Services;

public interface IApplicationQueryService
{
    /// <summary>Single IQueryable builder reused by the server-rendered list (Epic 8)
    /// and, unchanged, by Bonus 1's paged JSON endpoint (Epic 10) — filters/scope/sort
    /// live once. Not materialized here; callers apply Skip/Take/Select and a separate
    /// CountAsync (before Skip/Take) themselves. <paramref name="sortKey"/> is
    /// allow-listed inside the implementation — never pass a client value straight
    /// into an OrderBy expression.</summary>
    IQueryable<Application> BuildQuery(
        string userId,
        bool isApplicant,
        ApplicationStatus? status = null,
        int? propertyId = null,
        string? sortKey = null,
        bool descending = false,
        string? search = null);

    /// <summary>Applicant wizard/detail load — ownership-scoped Includes.</summary>
    Task<Application?> GetOwnedWithDetailsAsync(int id, string userId, CancellationToken ct = default);

    /// <summary>PM detail/review load — any application, no ownership filter.</summary>
    Task<Application?> GetForPmWithDetailsAsync(int id, CancellationToken ct = default);

    Task<string?> GetClaimedByUserIdAsync(int id, CancellationToken ct = default);

    Task<bool> ExistsAsync(int id, CancellationToken ct = default);

    /// <summary>Tracked entity for note create (mutation via <see cref="IApplicationService"/>).</summary>
    Task<Application?> GetTrackedByIdAsync(int id, CancellationToken ct = default);

    Task<Residence?> GetResidenceAsNoTrackingAsync(int residenceId, CancellationToken ct = default);

    Task<ApplicationNote?> GetNoteAsync(int applicationId, int noteId, bool asNoTracking = false, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, string>> GetUserDisplayNamesAsync(IEnumerable<string> userIds, CancellationToken ct = default);

    Task<IReadOnlyList<ApplicationNote>> GetNotesAsync(int applicationId, CancellationToken ct = default);

    Task<string?> GetLatestReturnCommentAsync(int applicationId, CancellationToken ct = default);
}
