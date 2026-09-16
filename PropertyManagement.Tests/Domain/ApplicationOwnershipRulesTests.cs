using FluentAssertions;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Rules;
using Xunit;

namespace PropertyManagement.Tests.Domain;

public class ApplicationOwnershipRulesTests
{
    [Fact]
    public void IsOwnedBy_UserInApplicantSet_ReturnsTrue()
    {
        var app = new Application();
        app.Applicants.Add(new ApplicationApplicant { UserId = "userA" });

        app.IsOwnedBy("userA").Should().BeTrue();
    }

    [Fact]
    public void IsOwnedBy_UserNotInApplicantSet_ReturnsFalse()
    {
        var app = new Application();
        app.Applicants.Add(new ApplicationApplicant { UserId = "userA" });

        app.IsOwnedBy("userB").Should().BeFalse();
    }

    [Fact]
    public void IsOwnedBy_MultipleApplicants_AnyMatchReturnsTrue()
    {
        var app = new Application();
        app.Applicants.Add(new ApplicationApplicant { UserId = "userA" });
        app.Applicants.Add(new ApplicationApplicant { UserId = "userB" });

        app.IsOwnedBy("userB").Should().BeTrue();
    }

    [Fact]
    public void IsOwnedBy_NoApplicants_ReturnsFalse()
    {
        var app = new Application();

        app.IsOwnedBy("userA").Should().BeFalse();
    }
}
