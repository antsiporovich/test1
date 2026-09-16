namespace PropertyManagement.Web.Models;

public class PropertyRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int UnitCount { get; set; }
}
