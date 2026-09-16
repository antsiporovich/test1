namespace PropertyManagement.Web.Models;

public class CoApplicantsListViewModel
{
    public int ApplicationId { get; set; }
    public bool IsEditable { get; set; }
    public List<CoApplicantRowViewModel> Applicants { get; set; } = [];
}
