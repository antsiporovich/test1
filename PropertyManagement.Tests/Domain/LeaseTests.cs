using FluentAssertions;
using PropertyManagement.Domain.Entities;
using Xunit;

namespace PropertyManagement.Tests.Domain;

public class LeaseTests
{
    [Fact]
    public void Create_EndDateIsTwelveMonthsAfterStart()
    {
        var start = new DateOnly(2026, 3, 1);
        var createdAt = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var lease = Lease.Create(unitId: 1, applicationId: 2, start, createdAt);

        lease.UnitId.Should().Be(1);
        lease.ApplicationId.Should().Be(2);
        lease.StartDate.Should().Be(start);
        lease.EndDate.Should().Be(new DateOnly(2027, 3, 1));
    }
}
