namespace PropertyManagement.Web.Models;

public class ResidenceHistoryListViewModel
{
    public int ApplicationId { get; set; }
    public bool IsEditable { get; set; }
    public List<ResidenceRowViewModel> Residences { get; set; } = [];
}
