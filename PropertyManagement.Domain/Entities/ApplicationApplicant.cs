namespace PropertyManagement.Domain.Entities;

/// <summary>
/// Join table for Bonus 5 multi-applicant support (Features/14, MULTI-1). Ownership
/// checks everywhere should test "is the current user in this set" rather than a single
/// owner field.
/// </summary>
public class ApplicationApplicant
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }
    public Application Application { get; set; } = null!;

    /// <summary>FK to AspNetUsers.Id (ApplicationUser lives in Infrastructure, so this
    /// is a plain string, not a navigation property, to keep Domain free of Identity).</summary>
    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset AddedAtUtc { get; set; }
}
