using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Rules;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class UnitService(AppDbContext db, TimeProvider timeProvider) : IUnitService
{
    public async Task<Unit?> GetActiveByIdAsync(int unitId, CancellationToken ct = default) =>
        await db.Units
            .Include(u => u.UnitType)
            .Include(u => u.Property)
            .Where(u => u.IsActive && u.Id == unitId)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

    public async Task<List<UnitType>> GetSelectableUnitTypesAsync(int? currentUnitTypeId, CancellationToken ct = default) =>
        await db.UnitTypes
            .Where(t => t.IsActive || t.Id == currentUnitTypeId)
            .OrderBy(t => t.Name)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<ServiceResult<Unit>> CreateAsync(UnitInput input, CancellationToken ct = default)
    {
        var property = await db.Properties.FirstOrDefaultAsync(p => p.IsActive && p.Id == input.PropertyId, ct);
        if (property is null)
        {
            return ServiceResult<Unit>.Fail(string.Empty, "Property not found.");
        }

        var duplicateCheck = await ValidateUniqueUnitNumberAsync(input.PropertyId, input.UnitNumber, excludingUnitId: null, ct);
        if (duplicateCheck is not null)
        {
            return duplicateCheck;
        }

        var unitType = await db.UnitTypes.FirstOrDefaultAsync(t => t.Id == input.UnitTypeId, ct);
        if (unitType is null)
        {
            return ServiceResult<Unit>.Fail(nameof(UnitInput.UnitTypeId), "Select a unit type.");
        }

        if (!UnitTypeAssignmentRules.CanAssignUnitType(currentUnitTypeId: null, input.UnitTypeId, unitType.IsActive))
        {
            return ServiceResult<Unit>.Fail(nameof(UnitInput.UnitTypeId), "This unit type is inactive and cannot be assigned to a new unit.");
        }

        var unit = new Unit
        {
            PropertyId = input.PropertyId,
            UnitNumber = input.UnitNumber,
            Bedrooms = input.Bedrooms,
            Bathrooms = input.Bathrooms,
            MonthlyRent = input.MonthlyRent,
            UnitTypeId = input.UnitTypeId,
            IsActive = true,
        };

        db.Units.Add(unit);
        await db.SaveChangesAsync(ct);
        return ServiceResult<Unit>.Success(unit);
    }

    public async Task<ServiceResult<Unit>> UpdateAsync(int unitId, UnitInput input, CancellationToken ct = default)
    {
        var unit = await db.Units.FirstOrDefaultAsync(u => u.IsActive && u.Id == unitId, ct);
        if (unit is null)
        {
            return ServiceResult<Unit>.Fail(string.Empty, "Unit not found.");
        }

        var duplicateCheck = await ValidateUniqueUnitNumberAsync(unit.PropertyId, input.UnitNumber, excludingUnitId: unitId, ct);
        if (duplicateCheck is not null)
        {
            return duplicateCheck;
        }

        var unitType = await db.UnitTypes.FirstOrDefaultAsync(t => t.Id == input.UnitTypeId, ct);
        if (unitType is null)
        {
            return ServiceResult<Unit>.Fail(nameof(UnitInput.UnitTypeId), "Select a unit type.");
        }

        if (!UnitTypeAssignmentRules.CanAssignUnitType(unit.UnitTypeId, input.UnitTypeId, unitType.IsActive))
        {
            return ServiceResult<Unit>.Fail(nameof(UnitInput.UnitTypeId), "This unit type is inactive and cannot be assigned to another unit.");
        }

        unit.UnitNumber = input.UnitNumber;
        unit.Bedrooms = input.Bedrooms;
        unit.Bathrooms = input.Bathrooms;
        unit.MonthlyRent = input.MonthlyRent;
        unit.UnitTypeId = input.UnitTypeId;

        await db.SaveChangesAsync(ct);
        return ServiceResult<Unit>.Success(unit);
    }

    public async Task<ServiceResult<bool>> RemoveAsync(int unitId, CancellationToken ct = default)
    {
        var unit = await db.Units.FirstOrDefaultAsync(u => u.IsActive && u.Id == unitId, ct);
        if (unit is null)
        {
            return ServiceResult<bool>.Fail(string.Empty, "Unit not found.");
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var hasActiveLease = await db.Leases.Where(l => l.UnitId == unitId).CoveringDate(today).AnyAsync(ct);
        if (hasActiveLease)
        {
            return ServiceResult<bool>.Fail(string.Empty, "This unit has an active lease and cannot be removed.");
        }

        var hasOpenApplication = await db.Applications
            .Where(a => a.UnitId == unitId && !ApplicationStatusRules.TerminalStatuses.Contains(a.Status))
            .AnyAsync(ct);
        if (hasOpenApplication)
        {
            return ServiceResult<bool>.Fail(string.Empty, "This unit has an open application and cannot be removed.");
        }

        unit.IsActive = false;
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<IReadOnlyList<UnitAvailabilityRow>> GetActiveWithAvailabilityAsync(int propertyId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var units = await db.Units
            .Where(u => u.IsActive && u.PropertyId == propertyId)
            .Include(u => u.UnitType)
            .OrderBy(u => u.UnitNumber)
            .AsNoTracking()
            .ToListAsync(ct);

        var unitIds = units.Select(u => u.Id).ToList();
        var unavailableUnitIds = await db.Leases
            .Where(l => unitIds.Contains(l.UnitId))
            .CoveringDate(today)
            .Select(l => l.UnitId)
            .ToListAsync(ct);

        return units
            .Select(u => new UnitAvailabilityRow(u, IsAvailable: !unavailableUnitIds.Contains(u.Id)))
            .ToList();
    }

    private async Task<ServiceResult<Unit>?> ValidateUniqueUnitNumberAsync(int propertyId, string unitNumber, int? excludingUnitId, CancellationToken ct)
    {
        var isDuplicate = await db.Units.AnyAsync(
            u => u.PropertyId == propertyId
                && u.UnitNumber == unitNumber
                && (excludingUnitId == null || u.Id != excludingUnitId.Value),
            ct);

        return isDuplicate
            ? ServiceResult<Unit>.Fail(nameof(UnitInput.UnitNumber), "A unit with this number already exists in this property.")
            : null;
    }
}
