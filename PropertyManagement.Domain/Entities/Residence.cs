namespace PropertyManagement.Domain.Entities;

/// <summary>
/// One prior residence, added/edited/removed via its own modal (Features/05). Own
/// concurrency token so two co-applicants editing different residences (or the same one)
/// are checked at the correct granularity (Features/14, MULTI-3/MULTI-4).
/// </summary>
public class Residence
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }
    public Application Application { get; set; } = null!;

    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    public string LandlordName { get; set; } = string.Empty;
    public string LandlordPhone { get; set; } = string.Empty;

    public DateOnly MoveInDate { get; set; }
    public DateOnly? MoveOutDate { get; set; }

    public decimal? MonthlyRent { get; set; }
    public string? ReasonForLeaving { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
