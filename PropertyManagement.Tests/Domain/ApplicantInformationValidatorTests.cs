using FluentAssertions;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Validation;
using Xunit;

namespace PropertyManagement.Tests.Domain;

public class ApplicantInformationValidatorTests
{
    private static ApplicantInfo Valid() => new()
    {
        FullName = "Alex Morgan",
        Phone = "555-123-4567",
        Email = "alex@example.com",
        AddressLine1 = "1 Main St",
        City = "Springfield",
        State = "IL",
        ZipCode = "62704",
    };

    [Fact]
    public void Validate_NullInfo_ReturnsNoErrors()
    {
        // The "not started" case is reported by ApplicationValidation at the section
        // level, not here — a null entity has no per-field data to complain about.
        ApplicantInformationValidator.Validate(null).Should().BeEmpty();
    }

    [Fact]
    public void Validate_AllFieldsPresentAndWellFormed_ReturnsNoErrors()
    {
        ApplicantInformationValidator.Validate(Valid()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(nameof(ApplicantInfo.FullName))]
    [InlineData(nameof(ApplicantInfo.Phone))]
    [InlineData(nameof(ApplicantInfo.Email))]
    [InlineData(nameof(ApplicantInfo.AddressLine1))]
    [InlineData(nameof(ApplicantInfo.City))]
    [InlineData(nameof(ApplicantInfo.State))]
    [InlineData(nameof(ApplicantInfo.ZipCode))]
    public void Validate_MissingRequiredField_ReturnsErrorForThatFieldOnly(string fieldName)
    {
        var info = Valid();
        typeof(ApplicantInfo).GetProperty(fieldName)!.SetValue(info, string.Empty);

        var errors = ApplicantInformationValidator.Validate(info).ToList();

        errors.Should().ContainSingle(e => e.Field == fieldName);
    }

    [Fact]
    public void Validate_MalformedEmail_ReturnsEmailError()
    {
        var info = Valid();
        info.Email = "not-an-email";

        ApplicantInformationValidator.Validate(info).Should().ContainSingle(e => e.Field == nameof(ApplicantInfo.Email));
    }

    [Fact]
    public void Validate_MultipleMissingFields_ReturnsOneErrorPerField()
    {
        var info = Valid();
        info.FullName = "";
        info.Email = "";

        var errors = ApplicantInformationValidator.Validate(info).ToList();

        errors.Should().HaveCount(2);
        errors.Select(e => e.Field).Should().BeEquivalentTo([nameof(ApplicantInfo.FullName), nameof(ApplicantInfo.Email)]);
    }
}
