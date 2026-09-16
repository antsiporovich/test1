namespace PropertyManagement.Web.Models.Api;

/// <summary>One row of the Bonus 1 grid JSON endpoint (Features/10, GRID-4).</summary>
public class ApplicationRowDto
{
    /// <summary>Application id — not rendered as a grid column, used for row-click navigation.</summary>
    public int Id { get; set; }

    public string? Applicant { get; set; }
    public string Property { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset Updated { get; set; }
}
