using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Domain.Rules;

/// <summary>
/// The one definition of "a lease covers this date" — used identically by unit
/// removal (Epic 2), browse availability (Epic 3), submit (Epic 6), and approval
/// (Epic 9), so all four agree with each other by construction rather than by
/// convention. <see cref="CoveringDate"/> is a plain <c>Where</c> composition (not
/// a wrapped method call), so it translates to SQL when used against an
/// <see cref="IQueryable{T}"/> the same way it behaves in memory.
/// </summary>
public static class LeaseAvailabilityRules
{
    public static bool CoversDate(this Lease lease, DateOnly date) =>
        lease.StartDate <= date && date <= lease.EndDate;

    public static IQueryable<Lease> CoveringDate(this IQueryable<Lease> leases, DateOnly date) =>
        leases.Where(l => l.StartDate <= date && date <= l.EndDate);
}
