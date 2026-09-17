using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class ApplicationQueryService(AppDbContext db) : IApplicationQueryService
{
    public IQueryable<Application> BuildQuery(
        string userId,
        bool isApplicant,
        ApplicationStatus? status = null,
        int? propertyId = null,
        string? sortKey = null,
        bool descending = false,
        string? search = null)
    {
        var query = db.Applications.AsQueryable();

        // Ownership scope applied first so a filter can never widen past it (LIST-1/LIST-3).
        if (isApplicant)
        {
            query = query.OwnedBy(userId);
        }

        if (status is not null)
        {
            query = query.Where(a => a.Status == status);
        }

        if (propertyId is not null)
        {
            query = query.Where(a => a.Unit.PropertyId == propertyId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(a => a.ApplicantInfo != null && a.ApplicantInfo.FullName.Contains(search));
        }

        return ApplySort(query, sortKey, descending);
    }

    public Task<Application?> GetOwnedWithDetailsAsync(int id, string userId, CancellationToken ct = default) =>
        db.Applications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.ApplicantInfo)
            .Include(a => a.Residences)
            .Include(a => a.Applicants)
            .Include(a => a.StatusHistory)
            .OwnedBy(userId)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Application?> GetForPmWithDetailsAsync(int id, CancellationToken ct = default) =>
        db.Applications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.Unit).ThenInclude(u => u.UnitType)
            .Include(a => a.ApplicantInfo)
            .Include(a => a.Residences)
            .Include(a => a.Applicants)
            .Include(a => a.StatusHistory)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<string?> GetClaimedByUserIdAsync(int id, CancellationToken ct = default) =>
        db.Applications.Where(a => a.Id == id).Select(a => (string?)a.ClaimedByUserId).FirstOrDefaultAsync(ct);

    public Task<bool> ExistsAsync(int id, CancellationToken ct = default) =>
        db.Applications.AnyAsync(a => a.Id == id, ct);

    public Task<Application?> GetTrackedByIdAsync(int id, CancellationToken ct = default) =>
        db.Applications.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Residence?> GetResidenceAsNoTrackingAsync(int residenceId, CancellationToken ct = default) =>
        db.Residences.AsNoTracking().FirstOrDefaultAsync(r => r.Id == residenceId, ct);

    public Task<ApplicationNote?> GetNoteAsync(int applicationId, int noteId, bool asNoTracking = false, CancellationToken ct = default)
    {
        IQueryable<ApplicationNote> query = db.ApplicationNotes;
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(n => n.Id == noteId && n.ApplicationId == applicationId, ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetUserDisplayNamesAsync(
        IEnumerable<string> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        return await db.Users
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? u.Email ?? u.UserName ?? u.Id, ct);
    }

    public async Task<IReadOnlyList<ApplicationNote>> GetNotesAsync(int applicationId, CancellationToken ct = default) =>
        await db.ApplicationNotes
            .AsNoTracking()
            .Where(n => n.ApplicationId == applicationId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(ct);

    public Task<string?> GetLatestReturnCommentAsync(int applicationId, CancellationToken ct = default) =>
        db.ApplicationStatusHistories
            .Where(h => h.ApplicationId == applicationId && h.ToStatus == ApplicationStatus.Returned)
            .OrderByDescending(h => h.Timestamp)
            .Select(h => h.Comment)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Residence>> GetResidencesForApplicationAsync(int applicationId, CancellationToken ct = default) =>
        await db.Residences
            .Where(r => r.ApplicationId == applicationId)
            .OrderByDescending(r => r.MoveInDate)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CoApplicantDto>> GetCoApplicantsAsync(int applicationId, CancellationToken ct = default) =>
        await db.ApplicationApplicants
            .Where(a => a.ApplicationId == applicationId)
            .Join(db.Users, a => a.UserId, u => u.Id, (a, u) => new CoApplicantDto(
                u.DisplayName ?? u.UserName ?? u.Id,
                u.Email ?? string.Empty))
            .OrderBy(a => a.DisplayName)
            .AsNoTracking()
            .ToListAsync(ct);

    // Allow-listed switch, never a client-provided column name interpolated into the
    // query (GRID-2). The "updated" expression is duplicated (not shared via a method
    // reference) with the DTO projection in ApplicationsApiController on purpose: EF
    // Core cannot translate a call to an external method inside an OrderBy/Select
    // expression tree, only inlined LINQ.
    private static IQueryable<Application> ApplySort(IQueryable<Application> query, string? sortKey, bool descending) => sortKey switch
    {
        "property" => descending
            ? query.OrderByDescending(a => a.Unit.Property.Name).ThenByDescending(a => a.Unit.UnitNumber)
            : query.OrderBy(a => a.Unit.Property.Name).ThenBy(a => a.Unit.UnitNumber),
        "status" => descending
            ? query.OrderByDescending(a => a.Status)
            : query.OrderBy(a => a.Status),
        "updated" => descending
            ? query.OrderByDescending(a => a.StatusHistory.Any() ? a.StatusHistory.Max(h => h.Timestamp) : a.CreatedAtUtc)
            : query.OrderBy(a => a.StatusHistory.Any() ? a.StatusHistory.Max(h => h.Timestamp) : a.CreatedAtUtc),
        _ => query.OrderByDescending(a => a.StatusHistory.Any() ? a.StatusHistory.Max(h => h.Timestamp) : a.CreatedAtUtc), // default: most recently active first
    };
}
