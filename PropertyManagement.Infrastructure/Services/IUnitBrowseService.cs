using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Services;

public interface IUnitBrowseService
{
    /// <summary>Active units with no lease covering today — SQL-side, via LeaseAvailabilityRules.</summary>
    Task<List<Unit>> GetAvailableUnitsAsync(CancellationToken ct = default);

    /// <summary>Re-checks availability server-side, resumes an existing open application for
    /// this unit+applicant instead of duplicating, else creates a Draft.</summary>
    Task<ServiceResult<Application>> ApplyAsync(int unitId, string userId, CancellationToken ct = default);
}
