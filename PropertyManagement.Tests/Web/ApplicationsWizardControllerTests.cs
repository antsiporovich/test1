using System.Net;
using FluentAssertions;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Tests.Web;

/// <summary>
/// Controller/integration coverage for graded wizard server rules (WIZ-5):
/// read-only section rendering + independent write-guard Forbid on illegal POSTs.
/// </summary>
public class ApplicationsWizardControllerTests : IClassFixture<PropertyManagementWebApplicationFactory>
{
    private readonly PropertyManagementWebApplicationFactory _factory;

    public ApplicationsWizardControllerTests(PropertyManagementWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Wizard_Get_WhenSubmitted_RendersApplicantInfoReadOnlyNotInputs()
    {
        await _factory.EnsureCreatedAsync();
        var (user, _) = await _factory.SeedApplicantAsync($"submitted-get-{Guid.NewGuid():N}@test.local");
        var appId = await _factory.SeedApplicationAsync(user.Id, ApplicationStatus.Submitted);

        var client = _factory.CreateAuthenticatedClient(user);
        var response = await client.GetAsync($"/Applications/{appId}?step={WizardStep.ApplicantInformation}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("data-applicant-info-mode=\"readonly\"");
        html.Should().NotContain("data-applicant-info-mode=\"edit\"");
        html.Should().NotContain("name=\"ApplicantInformation.FullName\"");
        html.Should().Contain("Test Applicant");
    }

    [Fact]
    public async Task Wizard_Get_WhenDraft_RendersApplicantInfoEditableInputs()
    {
        await _factory.EnsureCreatedAsync();
        var (user, _) = await _factory.SeedApplicantAsync($"draft-get-{Guid.NewGuid():N}@test.local");
        var appId = await _factory.SeedApplicationAsync(user.Id, ApplicationStatus.Draft);

        var client = _factory.CreateAuthenticatedClient(user);
        var response = await client.GetAsync($"/Applications/{appId}?step={WizardStep.ApplicantInformation}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("data-applicant-info-mode=\"edit\"");
        html.Should().Contain("name=\"ApplicantInformation.FullName\"");
    }

    [Fact]
    public async Task Wizard_PostContinue_WhenSubmitted_IsForbidden()
    {
        await _factory.EnsureCreatedAsync();
        var (user, _) = await _factory.SeedApplicantAsync($"submitted-post-{Guid.NewGuid():N}@test.local");
        var appId = await _factory.SeedApplicationAsync(user.Id, ApplicationStatus.Submitted);

        var client = _factory.CreateAuthenticatedClient(user);
        var response = await client.PostAsync($"/Applications/{appId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["command"] = "Continue",
            ["CurrentStep"] = nameof(WizardStep.ApplicantInformation),
            ["ApplicantInformation.FullName"] = "Hacker",
            ["ApplicantInformation.Phone"] = "555-9999",
            ["ApplicantInformation.Email"] = "hack@test.local",
            ["ApplicantInformation.AddressLine1"] = "X",
            ["ApplicantInformation.City"] = "Y",
            ["ApplicantInformation.State"] = "Z",
            ["ApplicantInformation.ZipCode"] = "1",
            ["ApplicantInformation.DateOfBirth"] = "1990-01-01",
            ["ApplicantInformation.Employment"] = "X",
            ["ApplicantInformation.AnnualIncome"] = "1",
            ["ApplicantInformation.DesiredMoveInDate"] = "2030-01-01",
        }));

        // Cookie auth Forbid → redirect to AccessDenied; test scheme often returns 403.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect);
        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            response.Headers.Location!.ToString().Should().Contain("AccessDenied");
        }
    }

    [Fact]
    public async Task Wizard_Get_WhenNotOwner_ReturnsNotFound()
    {
        await _factory.EnsureCreatedAsync();
        var (owner, _) = await _factory.SeedApplicantAsync($"owner-{Guid.NewGuid():N}@test.local");
        var (other, _) = await _factory.SeedApplicantAsync($"other-{Guid.NewGuid():N}@test.local");
        var appId = await _factory.SeedApplicationAsync(owner.Id, ApplicationStatus.Draft);

        var client = _factory.CreateAuthenticatedClient(other);
        var response = await client.GetAsync($"/Applications/{appId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
