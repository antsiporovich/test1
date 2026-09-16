using FluentAssertions;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Validation;
using Xunit;

namespace PropertyManagement.Tests.Domain;

public class ApplicationValidationTests
{
    private static ApplicantInfo ValidApplicantInfo() => new()
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
    public void GetOutstandingErrors_NeitherSectionStarted_ReturnsOneErrorPerSection()
    {
        var app = new Application();

        var errors = ApplicationValidation.GetOutstandingErrors(app).ToList();

        errors.Should().HaveCount(2);
        errors.Select(e => e.Field).Should().BeEquivalentTo(["Applicant Information", "Residence History"]);
    }

    [Fact]
    public void GetOutstandingErrors_BothSectionsCompleteAndValid_ReturnsNoErrors()
    {
        var app = new Application
        {
            ApplicantInfo = ValidApplicantInfo(),
            ResidenceHistoryConfirmedAtUtc = DateTimeOffset.UtcNow,
        };

        ApplicationValidation.GetOutstandingErrors(app).Should().BeEmpty();
    }

    [Fact]
    public void GetOutstandingErrors_SectionsSavedButApplicantInfoIncomplete_SurfacesFieldError()
    {
        // Reachable only because of Bonus 4's save-with-errors (Features/13, VALID-1) —
        // ApplicantInfo exists (Continue was clicked) but is missing required data.
        var info = ValidApplicantInfo();
        info.Email = "";
        var app = new Application
        {
            ApplicantInfo = info,
            ResidenceHistoryConfirmedAtUtc = DateTimeOffset.UtcNow,
        };

        var errors = ApplicationValidation.GetOutstandingErrors(app).ToList();

        errors.Should().ContainSingle(e => e.Field == nameof(ApplicantInfo.Email));
    }

    [Fact]
    public void GetOutstandingErrors_ResidenceWithBadDates_SurfacesFieldError()
    {
        var app = new Application
        {
            ApplicantInfo = ValidApplicantInfo(),
            ResidenceHistoryConfirmedAtUtc = DateTimeOffset.UtcNow,
            Residences = [new Residence { MoveInDate = new DateOnly(2024, 6, 1), MoveOutDate = new DateOnly(2024, 1, 1) }],
        };

        ApplicationValidation.GetOutstandingErrors(app).Should().ContainSingle(e => e.Field == "Residence 1");
    }
}
