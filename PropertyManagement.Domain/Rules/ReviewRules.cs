using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Rules;

/// <summary>
/// Pure business rule for the PM review outcome (Features/07, REVIEW-1).
/// Shared by the Web ViewModel's IValidatableObject and the unit tests —
/// defined once, two consumers.
/// </summary>
public static class ReviewRules
{
    /// <summary>Returns true when the given outcome requires a non-empty comment
    /// (Return and Deny require it; Approve does not).</summary>
    public static bool CommentRequired(ReviewOutcome outcome) =>
        outcome is ReviewOutcome.Return or ReviewOutcome.Deny;
}
