using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Data;
using PropertyManagement.Infrastructure.Services;
using Xunit;

namespace PropertyManagement.Tests.Infrastructure;

public class ApplicationServiceTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    private static (AppDbContext Db, ApplicationService Service, Unit Unit) CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options);
        var property = new Property { Name = "P1", AddressLine1 = "1 A St", City = "C", State = "S", ZipCode = "00000" };
        var unitType = new UnitType { Name = "Studio", IsActive = true };
        var unit = new Unit { Property = property, UnitType = unitType, UnitNumber = "101", MonthlyRent = 1000 };
        db.Units.Add(unit);
        db.SaveChanges();
        return (db, new ApplicationService(db, TimeProvider.System), unit);
    }

    private static Application AddApplication(AppDbContext db, Unit unit, ApplicationStatus status, bool bothSectionsSaved)
    {
        var app = new Application { UnitId = unit.Id, Status = status, CreatedAtUtc = DateTimeOffset.UtcNow };
        if (bothSectionsSaved)
        {
            app.ApplicantInfo = new ApplicantInfo { FullName = "A", Phone = "1", Email = "a@b.com", AddressLine1 = "x", City = "y", State = "z", ZipCode = "0" };
            app.ResidenceHistoryConfirmedAtUtc = DateTimeOffset.UtcNow;
        }
        db.Applications.Add(app);
        db.SaveChanges();
        return app;
    }

    [Fact]
    public async Task Submit_SectionsNotBothSaved_IsRejected()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Draft, bothSectionsSaved: false);

        var result = await service.SubmitAsync(app, "userA");

        result.Succeeded.Should().BeFalse();
        app.Status.Should().Be(ApplicationStatus.Draft);
    }

    [Fact]
    public async Task Submit_ApplicantInfoSavedWithOutstandingFieldError_IsRejected()
    {
        // Reachable only because of Bonus 4's save-with-errors (Features/13, VALID-1) —
        // both sections have been "saved" (Continue was clicked on each) but the
        // ApplicantInfo row is missing a required field. VALID-3's Submit gate must
        // catch this even though the presence-only "both saved" check would pass it.
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Draft, bothSectionsSaved: true);
        app.ApplicantInfo!.Email = "";
        db.SaveChanges();

        var result = await service.SubmitAsync(app, "userA");

        result.Succeeded.Should().BeFalse();
        app.Status.Should().Be(ApplicationStatus.Draft);
    }

    [Fact]
    public async Task Submit_UnitHasActiveLease_RejectedAndStaysDraft()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Draft, bothSectionsSaved: true);
        db.Leases.Add(new Lease { UnitId = unit.Id, StartDate = Today.AddDays(-1), EndDate = Today.AddMonths(12) });
        db.SaveChanges();

        var result = await service.SubmitAsync(app, "userA");

        result.Succeeded.Should().BeFalse();
        app.Status.Should().Be(ApplicationStatus.Draft);
        db.ApplicationStatusHistories.Should().BeEmpty();
    }

    [Fact]
    public async Task Submit_Success_TransitionsAndRecordsHistory()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Draft, bothSectionsSaved: true);

        var result = await service.SubmitAsync(app, "userA");

        result.Succeeded.Should().BeTrue();
        app.Status.Should().Be(ApplicationStatus.Submitted);
        app.SubmittedAtUtc.Should().NotBeNull();
        db.ApplicationStatusHistories.Should().ContainSingle(h => h.ApplicationId == app.Id && h.ToStatus == ApplicationStatus.Submitted && h.ActorUserId == "userA");
    }

    [Fact]
    public async Task Submit_SiblingApplicationForSameUnit_IsUntouched()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Draft, bothSectionsSaved: true);
        var sibling = AddApplication(db, unit, ApplicationStatus.Draft, bothSectionsSaved: true);

        await service.SubmitAsync(app, "userA");

        sibling.Status.Should().Be(ApplicationStatus.Draft);
    }

    [Theory]
    [InlineData(ApplicationStatus.Draft)]
    [InlineData(ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Returned)]
    public async Task Withdraw_FromNonTerminalStatus_Succeeds(ApplicationStatus status)
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, status, bothSectionsSaved: true);

        var result = await service.WithdrawAsync(app, "userA");

        result.Succeeded.Should().BeTrue();
        app.Status.Should().Be(ApplicationStatus.Withdrawn);
        db.ApplicationStatusHistories.Should().ContainSingle(h => h.ToStatus == ApplicationStatus.Withdrawn);
    }

    [Theory]
    [InlineData(ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.Denied)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public async Task Withdraw_FromTerminalStatus_IsRejected(ApplicationStatus status)
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, status, bothSectionsSaved: true);

        var result = await service.WithdrawAsync(app, "userA");

        result.Succeeded.Should().BeFalse();
        app.Status.Should().Be(status);
    }

    [Fact]
    public async Task Withdraw_SiblingApplicationForSameUnit_IsUntouched()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Submitted, bothSectionsSaved: true);
        var sibling = AddApplication(db, unit, ApplicationStatus.Submitted, bothSectionsSaved: true);

        await service.WithdrawAsync(app, "userA");

        sibling.Status.Should().Be(ApplicationStatus.Submitted);
    }

    [Fact]
    public async Task AddResidence_PersistsUnderApplication()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Draft, bothSectionsSaved: false);
        var input = new ResidenceInput("1 Main St", null, "City", "ST", "00000", "Landlord", "555-1234", Today.AddYears(-1), null, []);

        var residence = await service.AddResidenceAsync(app, input);

        residence.Id.Should().BeGreaterThan(0);
        db.Residences.Should().ContainSingle(r => r.ApplicationId == app.Id && r.LandlordName == "Landlord");
    }

    [Fact]
    public async Task UpdateResidence_ChangesFields()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Draft, bothSectionsSaved: false);
        var residence = await service.AddResidenceAsync(app, new ResidenceInput("1 Main St", null, "City", "ST", "00000", "Old", "555-1234", Today.AddYears(-1), null, []));

        var result = await service.UpdateResidenceAsync(residence, new ResidenceInput("2 Main St", null, "City", "ST", "00000", "New", "555-5678", Today.AddYears(-1), Today.AddMonths(-1), residence.RowVersion));

        result.Succeeded.Should().BeTrue();
        residence.LandlordName.Should().Be("New");
        residence.MoveOutDate.Should().Be(Today.AddMonths(-1));
    }

    [Fact]
    public async Task RemoveResidence_DeletesRow()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Draft, bothSectionsSaved: false);
        var residence = await service.AddResidenceAsync(app, new ResidenceInput("1 Main St", null, "City", "ST", "00000", "L", "555-1234", Today.AddYears(-1), null, []));

        await service.RemoveResidenceAsync(residence);

        db.Residences.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveApplicantInfo_CreatesRowWithUpdatedTimestamp()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Draft, bothSectionsSaved: false);
        var input = new ApplicantInfoInput("Jane Doe", "555-1234", "jane@example.com", "1 Main St", null, "City", "ST", "00000", []);

        await service.SaveApplicantInfoAsync(app, input);

        app.ApplicantInfo.Should().NotBeNull();
        app.ApplicantInfo!.FullName.Should().Be("Jane Doe");
        app.ApplicantInfo.UpdatedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task ConfirmResidenceHistory_SetsMarker()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Draft, bothSectionsSaved: false);

        await service.ConfirmResidenceHistoryAsync(app);

        app.ResidenceHistoryConfirmedAtUtc.Should().NotBeNull();
    }
}
