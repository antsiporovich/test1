using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Validation;

namespace PropertyManagement.Web.Models;

public class ApplicationWizardViewModel
{
    public int ApplicationId { get; set; }
    public WizardStep CurrentStep { get; set; }
    public bool IsEditable { get; set; }
    public bool IsWithdrawable { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReturnComment { get; set; }
    public string? SubmitError { get; set; }

    public ApplicantInfoSectionViewModel ApplicantInformation { get; set; } = new();

    /// <summary>Populated only for the Summary step's read-only recap.</summary>
    public List<ResidenceRowViewModel> Residences { get; set; } = [];

    /// <summary>Populated only for the Summary step (Features/13, VALID-3) — every
    /// issue still blocking Submit, from the same validators that produced any
    /// inline field errors.</summary>
    public List<FieldError> OutstandingErrors { get; set; } = [];
}
