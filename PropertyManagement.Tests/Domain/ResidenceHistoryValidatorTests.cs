using FluentAssertions;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Validation;
using Xunit;

namespace PropertyManagement.Tests.Domain;

public class ResidenceHistoryValidatorTests
{
    [Fact]
    public void Validate_EmptyList_ReturnsNoErrors()
    {
        // Zero prior residences is valid (Features/04, WIZ-7) — not this validator's concern.
        ResidenceHistoryValidator.Validate([]).Should().BeEmpty();
    }

    [Fact]
    public void Validate_MoveOutBeforeMoveIn_ReturnsError()
    {
        var residence = new Residence { MoveInDate = new DateOnly(2024, 6, 1), MoveOutDate = new DateOnly(2024, 5, 1) };

        ResidenceHistoryValidator.Validate([residence]).Should().ContainSingle();
    }

    [Fact]
    public void Validate_MoveOutOnOrAfterMoveIn_ReturnsNoError()
    {
        var residence = new Residence { MoveInDate = new DateOnly(2024, 6, 1), MoveOutDate = new DateOnly(2024, 6, 1) };

        ResidenceHistoryValidator.Validate([residence]).Should().BeEmpty();
    }

    [Fact]
    public void Validate_NullMoveOut_ReturnsNoError()
    {
        var residence = new Residence { MoveInDate = new DateOnly(2024, 6, 1), MoveOutDate = null };

        ResidenceHistoryValidator.Validate([residence]).Should().BeEmpty();
    }

    [Fact]
    public void Validate_MultipleResidences_IdentifiesTheBadOneBySpecificItem()
    {
        var good = new Residence { MoveInDate = new DateOnly(2020, 1, 1), MoveOutDate = new DateOnly(2021, 1, 1) };
        var bad = new Residence { MoveInDate = new DateOnly(2022, 1, 1), MoveOutDate = new DateOnly(2021, 1, 1) };

        var errors = ResidenceHistoryValidator.Validate([good, bad]).ToList();

        errors.Should().ContainSingle();
        errors[0].Field.Should().Be("Residence 2");
    }
}
