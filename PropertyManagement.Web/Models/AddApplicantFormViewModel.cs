using System.ComponentModel.DataAnnotations;

namespace PropertyManagement.Web.Models;

public class AddApplicantFormViewModel
{
    public int ApplicationId { get; set; }

    [Required]
    [EmailAddress]
    [Display(Name = "Applicant's email")]
    public string Email { get; set; } = string.Empty;
}
