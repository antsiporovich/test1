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

    /// <summary>12-month term, inclusive end (matches LeaseAvailabilityRules' `&lt;=` on both ends).</summary>
    public static Lease Create(int unitId, int applicationId, DateOnly startDate, DateTimeOffset createdAtUtc) => new()
    {
        UnitId = unitId,
        ApplicationId = applicationId,
        StartDate = startDate,
        EndDate = startDate.AddMonths(12),
        CreatedAtUtc = createdAtUtc,
    };
}
