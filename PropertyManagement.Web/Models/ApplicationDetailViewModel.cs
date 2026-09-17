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

    public DateTimeOffset? SubmittedAtUtc { get; set; }

    public string PropertyName { get; set; } = "";
    public string PropertyAddressSummary { get; set; } = "";
    public string PropertyImageUrl { get; set; } = "";
    public string UnitNumber { get; set; } = "";
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public string? UnitTypeName { get; set; }

    public string PrimaryApplicantName { get; set; } = "";
    public string PrimaryApplicantEmail { get; set; } = "";
    public string PrimaryApplicantPhone { get; set; } = "";
    public string PrimaryApplicantInitials { get; set; } = "";

    /// <summary>True when the current PM may open the Review modal:
    /// application is Submitted, or UnderReview and claimed by this PM.</summary>
    public bool CanReview { get; set; }

    // Applicant Information (read-only)
    public ApplicantInfoSectionViewModel? ApplicantInformation { get; set; }

    // Residence History (read-only)
    public List<ResidenceRowViewModel> Residences { get; set; } = [];

    // Status/review history — visible to PMs only (REVIEW-5).
    public List<StatusHistoryRowViewModel> StatusHistory { get; set; } = [];

    /// <summary>NOTES-1/NOTES-2: PM-only internal notes. Structurally absent from
    /// any Applicant-facing view model (ApplicationWizardViewModel never has this
    /// property — see NOTES-1 test in ApplicationServiceNoteTests).</summary>
    public List<NoteRowViewModel> Notes { get; set; } = [];
}
