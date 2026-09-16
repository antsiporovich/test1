using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Data;
using PropertyManagement.Infrastructure.Services;
using Xunit;

namespace PropertyManagement.Tests.Infrastructure;

public class ApplicationQueryServiceTests
{
    private static AppDbContext CreateSeededContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);

        var p1 = new Property { Name = "P1", AddressLine1 = "1 A St", City = "C", State = "S", ZipCode = "00000" };
        var p2 = new Property { Name = "P2", AddressLine1 = "2 B St", City = "C", State = "S", ZipCode = "00000" };
        var unitType = new UnitType { Name = "Studio", IsActive = true };
        var u1 = new Unit { Property = p1, UnitNumber = "101", UnitType = unitType, MonthlyRent = 1000 };
        var u2 = new Unit { Property = p2, UnitNumber = "201", UnitType = unitType, MonthlyRent = 1200 };

        var app1 = new Application { Unit = u1, Status = ApplicationStatus.Draft, CreatedAtUtc = DateTimeOffset.UtcNow };
        app1.Applicants.Add(new ApplicationApplicant { UserId = "userA", AddedAtUtc = DateTimeOffset.UtcNow });

        var app2 = new Application { Unit = u2, Status = ApplicationStatus.Submitted, CreatedAtUtc = DateTimeOffset.UtcNow };
        app2.Applicants.Add(new ApplicationApplicant { UserId = "userB", AddedAtUtc = DateTimeOffset.UtcNow });

        var app3 = new Application { Unit = u1, Status = ApplicationStatus.Submitted, CreatedAtUtc = DateTimeOffset.UtcNow };
        app3.Applicants.Add(new ApplicationApplicant { UserId = "userA", AddedAtUtc = DateTimeOffset.UtcNow });
        app3.Applicants.Add(new ApplicationApplicant { UserId = "userC", AddedAtUtc = DateTimeOffset.UtcNow });

        db.Applications.AddRange(app1, app2, app3);
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task BuildQuery_Applicant_ScopedToOwnApplications()
    {
        using var db = CreateSeededContext();
        var service = new ApplicationQueryService(db);

        var result = await service.BuildQuery("userA", isApplicant: true).ToListAsync();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.Applicants.Any(x => x.UserId == "userA"));
    }

    [Fact]
    public async Task BuildQuery_PropertyManager_ReturnsAllApplications()
    {
        using var db = CreateSeededContext();
        var service = new ApplicationQueryService(db);

        var result = await service.BuildQuery("anyUserId", isApplicant: false).ToListAsync();

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task BuildQuery_StatusFilter_NarrowsToMatchingStatus()
    {
        using var db = CreateSeededContext();
        var service = new ApplicationQueryService(db);

        var result = await service.BuildQuery("anyUserId", isApplicant: false, status: ApplicationStatus.Submitted).ToListAsync();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.Status == ApplicationStatus.Submitted);
    }

    [Fact]
    public async Task BuildQuery_PropertyFilter_NarrowsToUnitsUnderThatProperty()
    {
        using var db = CreateSeededContext();
        var service = new ApplicationQueryService(db);
        var p1Id = db.Properties.Single(p => p.Name == "P1").Id;

        var result = await service.BuildQuery("anyUserId", isApplicant: false, propertyId: p1Id).ToListAsync();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.Unit.PropertyId == p1Id);
    }

    [Fact]
    public async Task BuildQuery_CombinedFilters_ComposeAsIntersection()
    {
        using var db = CreateSeededContext();
        var service = new ApplicationQueryService(db);
        var p1Id = db.Properties.Single(p => p.Name == "P1").Id;

        var result = await service.BuildQuery("anyUserId", isApplicant: false, status: ApplicationStatus.Submitted, propertyId: p1Id).ToListAsync();

        result.Should().ContainSingle();
        result[0].Applicants.Select(a => a.UserId).Should().Contain("userC");
    }

    [Fact]
    public async Task BuildQuery_ApplicantWithFilter_NeverReturnsAnotherUsersApplication()
    {
        using var db = CreateSeededContext();
        var service = new ApplicationQueryService(db);

        var result = await service.BuildQuery("userB", isApplicant: true, status: ApplicationStatus.Submitted).ToListAsync();

        result.Should().ContainSingle();
        result[0].Applicants.Should().OnlyContain(a => a.UserId == "userB");
    }
}
