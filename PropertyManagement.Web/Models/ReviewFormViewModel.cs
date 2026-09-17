using System.ComponentModel.DataAnnotations;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;

namespace PropertyManagement.Web.Models;

/// <summary>
/// REVIEW-1: outcome radio group + comment. Comment is required for Return and Deny
/// (IValidatableObject delegates to ReviewRules.CommentRequired — one rule, two
/// consumers: this ViewModel and the unit test in ApplicationServiceReviewTests).
/// </summary>
public class ReviewFormViewModel : IValidatableObject
{
    public int ApplicationId { get; set; }

    /// <summary>Display-only header context (applicant name + property/unit). Set by the
    /// controller for GET and re-set on invalid POST; never bound from the request.</summary>
    public string ApplicantName { get; set; } = string.Empty;
    public string PropertyUnit { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select an outcome.")]
    public ReviewOutcome? Outcome { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Outcome.HasValue && ReviewRules.CommentRequired(Outcome.Value) && string.IsNullOrWhiteSpace(Comment))
        {
            yield return new ValidationResult(
                "A comment is required when returning or denying an application.",
                [nameof(Comment)]);
        }
    }
}
