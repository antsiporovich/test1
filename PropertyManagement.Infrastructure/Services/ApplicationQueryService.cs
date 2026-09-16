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
        bool descending = false)
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

        return ApplySort(query, sortKey, descending);
    }

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
