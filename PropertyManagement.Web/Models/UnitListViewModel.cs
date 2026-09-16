namespace PropertyManagement.Web.Models;

public class UnitListViewModel
{
    public int PropertyId { get; set; }
    public List<UnitRowViewModel> Units { get; set; } = [];
}
