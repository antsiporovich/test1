namespace PropertyManagement.Web.Models;

public class ApplicationListRowViewModel
{
    public int Id { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ApplicantName { get; set; }
    public DateTimeOffset LastUpdatedAtUtc { get; set; }
}
