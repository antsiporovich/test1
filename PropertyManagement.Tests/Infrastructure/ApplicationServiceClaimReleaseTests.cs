using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Data;
using PropertyManagement.Infrastructure.Identity;
using PropertyManagement.Infrastructure.Services;
using Xunit;

namespace PropertyManagement.Tests.Infrastructure;

// ExecuteUpdateAsync + BeginTransactionAsync (used for the atomic claim/release
// compare-and-set) aren't supported by the InMemory provider — SQLite in-memory
// per dotnet-unit-testing.md's transaction/concurrency guidance. The open
// connection must be kept alive for the test's lifetime (SqliteConnection is
// IDisposable, closing it drops the in-memory DB).
public class ApplicationServiceClaimReleaseTests
{
    private static (AppDbContext Db, ApplicationService Service, Unit Unit, SqliteConnection Connection) CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var property = new Property { Name = "P1", AddressLine1 = "1 A St", City = "C", State = "S", ZipCode = "00000" };
        var unitType = new UnitType { Name = "Studio", IsActive = true };
        var unit = new Unit { Property = property, UnitType = unitType, UnitNumber = "101", MonthlyRent = 1000 };
        db.Units.Add(unit);
        // SQLite (unlike InMemory) enforces the real FK from ActorUserId/ClaimedByUserId
        // to AspNetUsers, so the claiming/releasing PMs must exist.
        db.Users.AddRange(
            new ApplicationUser { Id = "pmA", UserName = "pmA", DisplayName = "PM A" },
            new ApplicationUser { Id = "pmB", UserName = "pmB", DisplayName = "PM B" });
        db.SaveChanges();
        return (db, new ApplicationService(db, TimeProvider.System), unit, connection);
    }

    private static Application AddApplication(AppDbContext db, Unit unit, ApplicationStatus status, string? claimedByUserId = null)
    {
        var app = new Application
        {
            UnitId = unit.Id,
            Status = status,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ClaimedByUserId = claimedByUserId,
            ClaimedAtUtc = claimedByUserId is not null ? DateTimeOffset.UtcNow : null,
        };
        db.Applications.Add(app);
        db.SaveChanges();
        return app;
    }

    [Fact]
    public async Task Claim_SubmittedApplication_Succeeds()
    {
        var (db, service, unit, connection) = CreateContext();
        using var _ = connection;
        var app = AddApplication(db, unit, ApplicationStatus.Submitted);

        var result = await service.ClaimAsync(app.Id, "pmA");

        result.Succeeded.Should().BeTrue();
        var reloaded = await db.Applications.AsNoTracking().SingleAsync(a => a.Id == app.Id);
        reloaded.Status.Should().Be(ApplicationStatus.UnderReview);
        reloaded.ClaimedByUserId.Should().Be("pmA");
        reloaded.ClaimedAtUtc.Should().NotBeNull();
        db.ApplicationStatusHistories.Should().ContainSingle(h => h.ApplicationId == app.Id && h.ToStatus == ApplicationStatus.UnderReview && h.ActorUserId == "pmA");
    }

    [Fact]
    public async Task Claim_AlreadyUnderReview_IsRejected_NoSilentTakeover()
    {
        var (db, service, unit, connection) = CreateContext();
        using var _ = connection;
        var app = AddApplication(db, unit, ApplicationStatus.UnderReview, claimedByUserId: "pmA");

        var result = await service.ClaimAsync(app.Id, "pmB");

        result.Succeeded.Should().BeFalse();
        var reloaded = await db.Applications.AsNoTracking().SingleAsync(a => a.Id == app.Id);
        reloaded.ClaimedByUserId.Should().Be("pmA"); // untouched by pmB's failed attempt
    }

    [Fact]
    public async Task Claim_SecondSequentialAttempt_LosesTheRace()
    {
        // Simulates two PMs claiming "simultaneously": the atomic conditional UPDATE's
        // WHERE clause is re-evaluated against the current DB state at execution time,
        // so calling it twice in a row faithfully reproduces "second caller loses"
        // without needing real multi-threading (which InMemory couldn't provide anyway).
        var (db, service, unit, connection) = CreateContext();
        using var _ = connection;
        var app = AddApplication(db, unit, ApplicationStatus.Submitted);

        var first = await service.ClaimAsync(app.Id, "pmA");
        var second = await service.ClaimAsync(app.Id, "pmB");

        first.Succeeded.Should().BeTrue();
        second.Succeeded.Should().BeFalse();
        var reloaded = await db.Applications.AsNoTracking().SingleAsync(a => a.Id == app.Id);
        reloaded.ClaimedByUserId.Should().Be("pmA");
        db.ApplicationStatusHistories.Where(h => h.ApplicationId == app.Id).Should().ContainSingle();
    }

    [Fact]
    public async Task Claim_NonExistentApplication_Fails()
    {
        var (db, service, unit, connection) = CreateContext();
        using var _ = connection;

        var result = await service.ClaimAsync(999, "pmA");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Release_ByClaimant_ReturnsToSubmittedAndClearsClaim()
    {
        var (db, service, unit, connection) = CreateContext();
        using var _ = connection;
        var app = AddApplication(db, unit, ApplicationStatus.UnderReview, claimedByUserId: "pmA");

        var result = await service.ReleaseAsync(app.Id, "pmA");

        result.Succeeded.Should().BeTrue();
        var reloaded = await db.Applications.AsNoTracking().SingleAsync(a => a.Id == app.Id);
        reloaded.Status.Should().Be(ApplicationStatus.Submitted);
        reloaded.ClaimedByUserId.Should().BeNull();
        reloaded.ClaimedAtUtc.Should().BeNull();
        db.ApplicationStatusHistories.Should().ContainSingle(h => h.ApplicationId == app.Id && h.ToStatus == ApplicationStatus.Submitted);
    }

    [Fact]
    public async Task Release_ByNonClaimant_IsRejected()
    {
        var (db, service, unit, connection) = CreateContext();
        using var _ = connection;
        var app = AddApplication(db, unit, ApplicationStatus.UnderReview, claimedByUserId: "pmA");

        var result = await service.ReleaseAsync(app.Id, "pmB");

        result.Succeeded.Should().BeFalse();
        var reloaded = await db.Applications.AsNoTracking().SingleAsync(a => a.Id == app.Id);
        reloaded.Status.Should().Be(ApplicationStatus.UnderReview);
        reloaded.ClaimedByUserId.Should().Be("pmA");
    }

    [Fact]
    public async Task Release_SiblingApplication_IsUntouched()
    {
        var (db, service, unit, connection) = CreateContext();
        using var _ = connection;
        var app = AddApplication(db, unit, ApplicationStatus.UnderReview, claimedByUserId: "pmA");
        var sibling = AddApplication(db, unit, ApplicationStatus.UnderReview, claimedByUserId: "pmA");

        await service.ReleaseAsync(app.Id, "pmA");

        var reloadedSibling = await db.Applications.AsNoTracking().SingleAsync(a => a.Id == sibling.Id);
        reloadedSibling.Status.Should().Be(ApplicationStatus.UnderReview);
        reloadedSibling.ClaimedByUserId.Should().Be("pmA");
    }
}
