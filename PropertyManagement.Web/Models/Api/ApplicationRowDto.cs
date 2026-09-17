namespace PropertyManagement.Web.Models.Api;

/// <summary>One row of the Bonus 1 grid JSON endpoint (Features/10, GRID-4).</summary>
public class ApplicationRowDto
{
    /// <summary>Application id — not rendered as a grid column, used for row-click navigation.</summary>
    public int Id { get; set; }

    public string? Applicant { get; set; }

    /// <summary>Combined "Property — Unit N" used by the PM grid's two-line column.</summary>
    public string Property { get; set; } = string.Empty;

    /// <summary>Property name and unit number as separate values, used by the applicant
    /// grid which renders them in distinct "Property" and "Unit #" columns.</summary>
    public string PropertyName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }

    public string Status { get; set; } = string.Empty;
    public DateTimeOffset Updated { get; set; }
}
