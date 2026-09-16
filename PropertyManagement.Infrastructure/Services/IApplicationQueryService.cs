using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Services;

public interface IApplicationQueryService
{
    /// <summary>Single IQueryable builder reused by the server-rendered list (Epic 8)
    /// and, unchanged, by Bonus 1's paged JSON endpoint (Epic 10) — filters/scope
    /// live once. Not materialized here; callers apply Skip/Take/OrderBy/Select
    /// before calling ToListAsync.</summary>
    IQueryable<Application> BuildQuery(string userId, bool isApplicant, ApplicationStatus? status = null, int? propertyId = null);
}
