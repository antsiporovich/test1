using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class UnitBrowseService(AppDbContext db, TimeProvider timeProvider) : IUnitBrowseService
{
    public async Task<List<Unit>> GetAvailableUnitsAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var unavailableUnitIds = db.Leases.CoveringDate(today).Select(l => l.UnitId);

        return await db.Units
            .Where(u => u.IsActive)
            .Where(u => !unavailableUnitIds.Contains(u.Id))
            .Include(u => u.Property)
            .Include(u => u.UnitType)
            .OrderBy(u => u.Property.Name).ThenBy(u => u.UnitNumber)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<ServiceResult<Application>> ApplyAsync(int unitId, string userId, CancellationToken ct = default)
    {
        var unit = await db.Units.FirstOrDefaultAsync(u => u.IsActive && u.Id == unitId, ct);
        if (unit is null)
        {
            return ServiceResult<Application>.Fail(string.Empty, "Unit not found.");
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var hasActiveLease = await db.Leases.Where(l => l.UnitId == unitId).CoveringDate(today).AnyAsync(ct);
        if (hasActiveLease)
        {
            return ServiceResult<Application>.Fail(string.Empty, "This unit is no longer available.");
        }

        var existing = await db.Applications
            .Where(a => a.UnitId == unitId)
            .OwnedBy(userId)
            .Where(a => !ApplicationStatusRules.TerminalStatuses.Contains(a.Status))
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return ServiceResult<Application>.Success(existing);
        }

        var now = timeProvider.GetUtcNow();
        var application = new Application
        {
            UnitId = unitId,
            Status = ApplicationStatus.Draft,
            CreatedAtUtc = now,
        };
        application.Applicants.Add(new ApplicationApplicant { UserId = userId, AddedAtUtc = now });

        db.Applications.Add(application);
        await db.SaveChangesAsync(ct);
        return ServiceResult<Application>.Success(application);
    }
}
