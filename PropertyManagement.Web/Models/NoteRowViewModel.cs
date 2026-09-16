namespace PropertyManagement.Web.Models;

/// <summary>NOTES-1/NOTES-2: one row in the PM-only notes list.
/// Never placed on any Applicant-facing view model.</summary>
public class NoteRowViewModel
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public string Body { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
