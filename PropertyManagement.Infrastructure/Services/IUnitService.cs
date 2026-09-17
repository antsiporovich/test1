using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Services;

public record UnitInput(int PropertyId, string UnitNumber, int Bedrooms, int Bathrooms, decimal MonthlyRent, int UnitTypeId);

public interface IUnitService
{
    Task<Unit?> GetActiveByIdAsync(int unitId, CancellationToken ct = default);

    /// <summary>Selectable unit types for a dropdown: Active ones, plus the unit's current
    /// type even if it has since gone Inactive (so it stays selected when left unchanged —
    /// Features/02 UNIT-1). Pass null for a brand-new unit (Active types only).</summary>
    Task<List<UnitType>> GetSelectableUnitTypesAsync(int? currentUnitTypeId, CancellationToken ct = default);

    Task<ServiceResult<Unit>> CreateAsync(UnitInput input, CancellationToken ct = default);

    Task<ServiceResult<Unit>> UpdateAsync(int unitId, UnitInput input, CancellationToken ct = default);

    /// <summary>Soft delete, rejected if the unit has an active lease or an open (non-terminal) application.</summary>
    Task<ServiceResult<bool>> RemoveAsync(int unitId, CancellationToken ct = default);

    /// <summary>Active units for a property (UnitType included), ordered by unit number —
    /// with live availability using the shared lease-covering-today predicate.</summary>
    Task<IReadOnlyList<UnitAvailabilityRow>> GetActiveWithAvailabilityAsync(int propertyId, CancellationToken ct = default);
}

/// <summary>Unit list row for the PM property units widget.</summary>
public record UnitAvailabilityRow(Unit Unit, bool IsAvailable);
