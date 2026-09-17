using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Services;

public interface IUnitBrowseService
{
    /// <summary>Active units with no lease covering today. When <paramref name="applicantUserId"/>
    /// is set, also excludes units that applicant already has an open (non-terminal) application for.</summary>
    Task<List<Unit>> GetAvailableUnitsAsync(string? applicantUserId = null, CancellationToken ct = default);

    /// <summary>Re-checks availability server-side, resumes an existing open application for
    /// this unit+applicant instead of duplicating, else creates a Draft.</summary>
    Task<ServiceResult<Application>> ApplyAsync(int unitId, string userId, CancellationToken ct = default);
}
