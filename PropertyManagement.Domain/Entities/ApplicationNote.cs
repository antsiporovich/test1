namespace PropertyManagement.Domain.Entities;

/// <summary>
/// Bonus 3 — property-manager-only notes. Never included in any applicant-facing view
/// model or DTO (Features/12, NOTES-1); enforce that at the Web layer, not here.
/// </summary>
public class ApplicationNote
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }
    public Application Application { get; set; } = null!;

    public string AuthorUserId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
