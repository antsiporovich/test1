using FluentAssertions;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Rules;
using Xunit;

namespace PropertyManagement.Tests.Domain;

public class LeaseAvailabilityRulesTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

    [Fact]
    public void CoversDate_LeaseStartingToday_CoversToday()
    {
        var lease = new Lease { StartDate = Today, EndDate = Today.AddMonths(12) };

        lease.CoversDate(Today).Should().BeTrue();
    }

    [Fact]
    public void CoversDate_LeaseEndingToday_CoversToday()
    {
        var lease = new Lease { StartDate = Today.AddMonths(-12), EndDate = Today };

        lease.CoversDate(Today).Should().BeTrue();
    }

    [Fact]
    public void CoversDate_FullyPastLease_DoesNotCoverToday()
    {
        var lease = new Lease { StartDate = Today.AddMonths(-14), EndDate = Today.AddDays(-1) };

        lease.CoversDate(Today).Should().BeFalse();
    }

    [Fact]
    public void CoversDate_FullyFutureLease_DoesNotCoverToday()
    {
        var lease = new Lease { StartDate = Today.AddDays(1), EndDate = Today.AddMonths(12) };

        lease.CoversDate(Today).Should().BeFalse();
    }
}
