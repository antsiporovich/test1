using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Rules;

/// <summary>
/// Re-derives the wizard's current step from persisted completion markers
/// (Features/04, WIZ-1/WIZ-4) — the step is never stored, always computed, so a
/// page refresh lands on saved progress. Reachable only while a step is being
/// entered for the first time; interactive Continue/Back during a session tracks
/// the step in a hidden form field instead (see ApplicationsController).
/// </summary>
public static class WizardStepRules
{
    public static WizardStep InitialStep(Application application)
    {
        if (application.ApplicantInfo is null)
        {
            return WizardStep.ApplicantInformation;
        }

        if (application.ResidenceHistoryConfirmedAtUtc is null)
        {
            return WizardStep.ResidenceHistory;
        }

        return WizardStep.Summary;
    }

    /// <summary>The Submit gate (WIZ-4) — re-checked server-side at submit time, never trusted from the client.</summary>
    public static bool BothSectionsSaved(Application application) =>
        application.ApplicantInfo is not null && application.ResidenceHistoryConfirmedAtUtc is not null;
}
