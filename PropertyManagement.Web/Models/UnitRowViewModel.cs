namespace PropertyManagement.Web.Models;

public class UnitRowViewModel
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string UnitNumber { get; set; } = string.Empty;
    public int Bedrooms { get; set; }
    public decimal MonthlyRent { get; set; }
    public string UnitTypeName { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
}
