namespace PropertyManagement.Domain.Entities;

/// <summary>Issued exactly once per approved Application (Features/09, LEASE-1).</summary>
public class Lease
{
    public int Id { get; set; }

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public int ApplicationId { get; set; }
    public Application Application { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
