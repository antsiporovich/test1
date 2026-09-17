using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Data;
using PropertyManagement.Infrastructure.Services;
using Xunit;

namespace PropertyManagement.Tests.Infrastructure;

public class UnitBrowseServiceTests
{
    private static readonly DateTimeOffset Now = TimeProvider.System.GetUtcNow();
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    private static (AppDbContext Db, UnitBrowseService Service, Property Property, UnitType UnitType) CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var time = TimeProvider.System;

        var property = new Property { Name = "P1", AddressLine1 = "1 A St", City = "C", State = "S", ZipCode = "00000" };
        var unitType = new UnitType { Name = "Studio", IsActive = true };
        db.Properties.Add(property);
        db.UnitTypes.Add(unitType);
        db.SaveChanges();

        return (db, new UnitBrowseService(db, time), property, unitType);
    }

    private static Unit AddUnit(AppDbContext db, Property property, UnitType unitType, string unitNumber) =>
        db.Units.Add(new Unit { Property = property, PropertyId = property.Id, UnitType = unitType, UnitTypeId = unitType.Id, UnitNumber = unitNumber, MonthlyRent = 1000 }).Entity;

    [Fact]
    public async Task GetAvailableUnits_ExcludesUnitWithLeaseCoveringToday()
    {
        var (db, service, property, unitType) = CreateContext();
        var covered = AddUnit(db, property, unitType, "101");
        var pastLease = AddUnit(db, property, unitType, "102");
        var futureLease = AddUnit(db, property, unitType, "103");
        var noLease = AddUnit(db, property, unitType, "104");
        db.SaveChanges();

        db.Leases.Add(new Lease { UnitId = covered.Id, StartDate = Today.AddDays(-10), EndDate = Today.AddDays(10) });
        db.Leases.Add(new Lease { UnitId = pastLease.Id, StartDate = Today.AddMonths(-14), EndDate = Today.AddDays(-1) });
        db.Leases.Add(new Lease { UnitId = futureLease.Id, StartDate = Today.AddDays(10), EndDate = Today.AddMonths(12) });
        db.SaveChanges();

        var result = await service.GetAvailableUnitsAsync();

        result.Select(u => u.UnitNumber).Should().BeEquivalentTo(["102", "103", "104"]);
    }

    [Fact]
    public async Task GetAvailableUnits_ExcludesUnitsApplicantAlreadyHasOpenApplicationFor()
    {
        var (db, service, property, unitType) = CreateContext();
        var openMine = AddUnit(db, property, unitType, "501");
        var terminalMine = AddUnit(db, property, unitType, "502");
        var openOther = AddUnit(db, property, unitType, "503");
        var untouched = AddUnit(db, property, unitType, "504");
        db.SaveChanges();

        var draft = new Application { UnitId = openMine.Id, Status = ApplicationStatus.Draft, CreatedAtUtc = Now };
        draft.Applicants.Add(new ApplicationApplicant { UserId = "userA", AddedAtUtc = Now });
        var withdrawn = new Application { UnitId = terminalMine.Id, Status = ApplicationStatus.Withdrawn, CreatedAtUtc = Now };
        withdrawn.Applicants.Add(new ApplicationApplicant { UserId = "userA", AddedAtUtc = Now });
        var otherDraft = new Application { UnitId = openOther.Id, Status = ApplicationStatus.Draft, CreatedAtUtc = Now };
        otherDraft.Applicants.Add(new ApplicationApplicant { UserId = "userB", AddedAtUtc = Now });
        db.Applications.AddRange(draft, withdrawn, otherDraft);
        db.SaveChanges();

        var result = await service.GetAvailableUnitsAsync("userA");

        result.Select(u => u.UnitNumber).Should().BeEquivalentTo(["502", "503", "504"]);
    }

    [Fact]
    public async Task Apply_UnitNowUnavailable_IsRejectedAndCreatesNoApplication()
    {
        var (db, service, property, unitType) = CreateContext();
        var unit = AddUnit(db, property, unitType, "201");
        db.SaveChanges();
        db.Leases.Add(new Lease { UnitId = unit.Id, StartDate = Today, EndDate = Today.AddMonths(12) });
        db.SaveChanges();

        var result = await service.ApplyAsync(unit.Id, "userA");

        result.Succeeded.Should().BeFalse();
        db.Applications.Should().BeEmpty();
    }

    [Fact]
    public async Task Apply_ExistingOpenApplicationForSameUnitAndApplicant_ResumesInsteadOfDuplicating()
    {
        var (db, service, property, unitType) = CreateContext();
        var unit = AddUnit(db, property, unitType, "301");
        db.SaveChanges();
        var existing = new Application { UnitId = unit.Id, Status = ApplicationStatus.Draft, CreatedAtUtc = Now };
        existing.Applicants.Add(new ApplicationApplicant { UserId = "userA", AddedAtUtc = Now });
        db.Applications.Add(existing);
        db.SaveChanges();

        var result = await service.ApplyAsync(unit.Id, "userA");

        result.Succeeded.Should().BeTrue();
        result.Value!.Id.Should().Be(existing.Id);
        db.Applications.Count(a => a.UnitId == unit.Id).Should().Be(1);
    }

    [Fact]
    public async Task Apply_NoExistingApplication_CreatesDraft()
    {
        var (db, service, property, unitType) = CreateContext();
        var unit = AddUnit(db, property, unitType, "401");
        db.SaveChanges();

        var result = await service.ApplyAsync(unit.Id, "userA");

        result.Succeeded.Should().BeTrue();
        result.Value!.Status.Should().Be(ApplicationStatus.Draft);
        db.Applications.Single().Applicants.Single().UserId.Should().Be("userA");
    }
}
