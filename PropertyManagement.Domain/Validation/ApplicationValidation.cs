using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Domain.Validation;

/// <summary>
/// Aggregates every outstanding issue across both wizard sections (Features/13,
/// VALID-2/VALID-3) — the one method shared by the Summary's outstanding-issues
/// list and the Submit gate, so they can never silently diverge.
/// </summary>
public static class ApplicationValidation
{
    public static IEnumerable<FieldError> GetOutstandingErrors(Application application)
    {
        if (application.ApplicantInfo is null)
        {
            yield return new FieldError("Applicant Information", "Applicant information has not been started.");
        }
        else
        {
            foreach (var error in ApplicantInformationValidator.Validate(application.ApplicantInfo))
            {
                yield return error;
            }
        }

        if (application.ResidenceHistoryConfirmedAtUtc is null)
        {
            yield return new FieldError("Residence History", "Residence history has not been confirmed.");
        }
        else
        {
            foreach (var error in ResidenceHistoryValidator.Validate(application.Residences))
            {
                yield return error;
            }
        }
    }
}
