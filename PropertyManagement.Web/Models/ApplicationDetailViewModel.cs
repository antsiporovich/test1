namespace PropertyManagement.Web.Models;

/// <summary>
/// PM-facing read-only detail view of a rental application (REVIEW-1/REVIEW-5).
/// Never surfaced to Applicant-facing views.
/// </summary>
public class ApplicationDetailViewModel
{
    public int Id { get; set; }
    public string PropertyUnit { get; set; } = "";
    public string ApplicantNames { get; set; } = "";
    public string Status { get; set; } = "";

    /// <summary>True when the current PM may open the Review modal:
    /// application is Submitted, or UnderReview and claimed by this PM.</summary>
    public bool CanReview { get; set; }

    // Applicant Information (read-only)
    public ApplicantInfoSectionViewModel? ApplicantInformation { get; set; }

    // Residence History (read-only)
    public List<ResidenceRowViewModel> Residences { get; set; } = [];

    // Status/review history — visible to PMs only (REVIEW-5).
    public List<StatusHistoryRowViewModel> StatusHistory { get; set; } = [];
}
