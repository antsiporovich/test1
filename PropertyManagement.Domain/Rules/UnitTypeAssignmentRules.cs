namespace PropertyManagement.Domain.Rules;

/// <summary>
/// Whether a unit type may be assigned to a unit (Features/02, UNIT-1): an
/// Inactive type stays valid on a unit that already uses it, but cannot be
/// newly assigned — to a new unit, or as a change away from a different type —
/// while Inactive. Pure and DB-free so it's directly unit-testable; callers
/// supply <paramref name="currentUnitTypeId"/> (null for a brand-new unit) and
/// the target type's current <paramref name="newUnitTypeIsActive"/> flag.
/// </summary>
public static class UnitTypeAssignmentRules
{
    public static bool CanAssignUnitType(int? currentUnitTypeId, int newUnitTypeId, bool newUnitTypeIsActive)
    {
        var isUnchanged = currentUnitTypeId.HasValue && currentUnitTypeId.Value == newUnitTypeId;
        return isUnchanged || newUnitTypeIsActive;
    }
}
