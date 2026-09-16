using FluentAssertions;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;
using Xunit;

namespace PropertyManagement.Tests.Domain;

public class WizardStepRulesTests
{
    [Fact]
    public void InitialStep_NoApplicantInfo_ReturnsApplicantInformation()
    {
        var app = new Application();

        WizardStepRules.InitialStep(app).Should().Be(WizardStep.ApplicantInformation);
    }

    [Fact]
    public void InitialStep_ApplicantInfoSavedResidenceNotConfirmed_ReturnsResidenceHistory()
    {
        var app = new Application { ApplicantInfo = new ApplicantInfo() };

        WizardStepRules.InitialStep(app).Should().Be(WizardStep.ResidenceHistory);
    }

    [Fact]
    public void InitialStep_BothSectionsSaved_ReturnsSummary()
    {
        var app = new Application { ApplicantInfo = new ApplicantInfo(), ResidenceHistoryConfirmedAtUtc = DateTimeOffset.UtcNow };

        WizardStepRules.InitialStep(app).Should().Be(WizardStep.Summary);
    }

    [Fact]
    public void BothSectionsSaved_FalseUntilBothMarkersSet()
    {
        var app = new Application();
        WizardStepRules.BothSectionsSaved(app).Should().BeFalse();

        app.ApplicantInfo = new ApplicantInfo();
        WizardStepRules.BothSectionsSaved(app).Should().BeFalse();

        app.ResidenceHistoryConfirmedAtUtc = DateTimeOffset.UtcNow;
        WizardStepRules.BothSectionsSaved(app).Should().BeTrue();
    }
}
