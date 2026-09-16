namespace PropertyManagement.Web.Models;

public class ResidenceRowViewModel
{
    public int Id { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string LandlordName { get; set; } = string.Empty;
    public string LandlordPhone { get; set; } = string.Empty;
    public DateOnly MoveInDate { get; set; }
    public DateOnly? MoveOutDate { get; set; }
}
