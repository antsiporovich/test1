using FluentAssertions;
using PropertyManagement.Domain.Rules;
using Xunit;

namespace PropertyManagement.Tests.Domain;

public class UnitTypeAssignmentRulesTests
{
    [Fact]
    public void CanAssignUnitType_NewUnit_ActiveType_IsAllowed()
    {
        var result = UnitTypeAssignmentRules.CanAssignUnitType(currentUnitTypeId: null, newUnitTypeId: 5, newUnitTypeIsActive: true);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanAssignUnitType_NewUnit_InactiveType_IsRejected()
    {
        var result = UnitTypeAssignmentRules.CanAssignUnitType(currentUnitTypeId: null, newUnitTypeId: 5, newUnitTypeIsActive: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanAssignUnitType_ChangingToDifferentInactiveType_IsRejected()
    {
        var result = UnitTypeAssignmentRules.CanAssignUnitType(currentUnitTypeId: 1, newUnitTypeId: 5, newUnitTypeIsActive: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanAssignUnitType_ChangingToDifferentActiveType_IsAllowed()
    {
        var result = UnitTypeAssignmentRules.CanAssignUnitType(currentUnitTypeId: 1, newUnitTypeId: 5, newUnitTypeIsActive: true);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanAssignUnitType_LeavingExistingInactiveTypeUnchanged_IsGrandfatheredAllowed()
    {
        var result = UnitTypeAssignmentRules.CanAssignUnitType(currentUnitTypeId: 5, newUnitTypeId: 5, newUnitTypeIsActive: false);

        result.Should().BeTrue();
    }
}
