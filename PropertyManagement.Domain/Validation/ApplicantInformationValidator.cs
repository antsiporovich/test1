using System.ComponentModel.DataAnnotations;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Domain.Validation;

/// <summary>
/// The single definition of "is Applicant Information complete" (Features/13,
/// VALID-2) — shared by the wizard's inline field errors and the Summary's
/// outstanding-issues list. Operates on the persisted entity, not the posted view
/// model, so it reflects save-with-errors data (Features/13, VALID-1). A null
/// entity (section never saved) is reported by the caller at the section level,
/// not here.
/// </summary>
public static class ApplicantInformationValidator
{
    private static readonly EmailAddressAttribute EmailRule = new();
    private static readonly PhoneAttribute PhoneRule = new();

    public static IEnumerable<FieldError> Validate(ApplicantInfo? info)
    {
        if (info is null)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(info.FullName))
        {
            yield return new FieldError(nameof(info.FullName), "Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(info.Phone))
        {
            yield return new FieldError(nameof(info.Phone), "Phone is required.");
        }
        else if (!PhoneRule.IsValid(info.Phone))
        {
            yield return new FieldError(nameof(info.Phone), "Phone is not a valid phone number.");
        }

        if (string.IsNullOrWhiteSpace(info.Email))
        {
            yield return new FieldError(nameof(info.Email), "Email is required.");
        }
        else if (!EmailRule.IsValid(info.Email))
        {
            yield return new FieldError(nameof(info.Email), "Email is not a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(info.AddressLine1))
        {
            yield return new FieldError(nameof(info.AddressLine1), "Address is required.");
        }

        if (string.IsNullOrWhiteSpace(info.City))
        {
            yield return new FieldError(nameof(info.City), "City is required.");
        }

        if (string.IsNullOrWhiteSpace(info.State))
        {
            yield return new FieldError(nameof(info.State), "State is required.");
        }

        if (string.IsNullOrWhiteSpace(info.ZipCode))
        {
            yield return new FieldError(nameof(info.ZipCode), "ZIP code is required.");
        }
    }
}
