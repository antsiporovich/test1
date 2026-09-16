using FluentAssertions;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;
using Xunit;

namespace PropertyManagement.Tests.Domain;

public class ApplicationStatusRulesTests
{
    [Theory]
    [InlineData(ApplicationStatus.Approved, true)]
    [InlineData(ApplicationStatus.Denied, true)]
    [InlineData(ApplicationStatus.Withdrawn, true)]
    [InlineData(ApplicationStatus.Draft, false)]
    [InlineData(ApplicationStatus.Submitted, false)]
    [InlineData(ApplicationStatus.UnderReview, false)]
    [InlineData(ApplicationStatus.Returned, false)]
    public void IsTerminal_ReturnsExpected(ApplicationStatus status, bool expected)
    {
        status.IsTerminal().Should().Be(expected);
    }

    [Theory]
    [InlineData(ApplicationStatus.Draft, true)]
    [InlineData(ApplicationStatus.Returned, true)]
    [InlineData(ApplicationStatus.Submitted, false)]
    [InlineData(ApplicationStatus.UnderReview, false)]
    [InlineData(ApplicationStatus.Approved, false)]
    [InlineData(ApplicationStatus.Denied, false)]
    [InlineData(ApplicationStatus.Withdrawn, false)]
    public void IsEditable_ReturnsExpected(ApplicationStatus status, bool expected)
    {
        status.IsEditable().Should().Be(expected);
    }
}
