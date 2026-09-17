using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Data;
using PropertyManagement.Infrastructure.Identity;
using PropertyManagement.Infrastructure.Services;
using Xunit;

namespace PropertyManagement.Tests.Infrastructure;

// Features/14 (Bonus 5): MULTI-3 (different sections don't interfere) and MULTI-4
// (same-section stale save rejected). InMemory does NOT enforce IsRowVersion()'s
// OriginalValue check (confirmed empirically — a stale save silently "succeeds"), per
// dotnet-unit-testing.md's documented caveat, so this uses SQLite in-memory instead,
// same pattern as Features/11's ApplicationServiceClaimReleaseTests.
public class ApplicationServiceConcurrencyTests
{
    private static (AppDbContext Db, ApplicationService Service, Application App, SqliteConnection Connection) CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        // SQLite has no native auto-updating rowversion column (unlike SQL Server): our
        // AppDbContext's randomblob(8) default only fires on INSERT. Without a trigger
        // regenerating it on UPDATE too, the column never changes, so a "stale" original
        // value would still match on a second save — these triggers make SQLite actually
        // emulate the versioning behavior being tested (recursive_triggers is off by
        // default in SQLite, so the trigger's own UPDATE doesn't re-fire itself).
        foreach (var table in new[] { "ApplicantInfos", "Residences" })
        {
            // Table names are fixed literals above, never external input.
#pragma warning disable EF1002
            db.Database.ExecuteSqlRaw($"""
                CREATE TRIGGER trg_{table}_RowVersion AFTER UPDATE ON {table}
                BEGIN
                    UPDATE {table} SET RowVersion = randomblob(8) WHERE rowid = NEW.rowid;
                END;
                """);
#pragma warning restore EF1002
        }

        var property = new Property { Name = "P1", AddressLine1 = "1 A St", City = "C", State = "S", ZipCode = "00000" };
        var unitType = new UnitType { Name = "Studio", IsActive = true };
        var unit = new Unit { Property = property, UnitType = unitType, UnitNumber = "101", MonthlyRent = 1000 };
        db.Units.Add(unit);
        var app = new Application { Unit = unit, Status = ApplicationStatus.Draft, CreatedAtUtc = DateTimeOffset.UtcNow };
        db.Applications.Add(app);
        db.SaveChanges();
        return (db, new ApplicationService(db, TimeProvider.System), app, connection);
    }

    private static ApplicantInfoInput ApplicantInput(string fullName, byte[] rowVersion) =>
        new(fullName, "555-1234", "a@example.com", "1 Main St", null, "City", "ST", "00000", new DateOnly(1990, 1, 1), "Engineer", 72000m, new DateOnly(2024, 6, 1), rowVersion);

    private static ResidenceInput ResidenceInput(string landlordName, byte[] rowVersion) =>
        new("1 Main St", null, "City", "ST", "00000", landlordName, "555-1234", new DateOnly(2020, 1, 1), null, 1200m, null, rowVersion);

    [Fact]
    public async Task SaveApplicantInfo_SecondSaveAgainstStaleRowVersion_IsRejected()
    {
        var (db, service, app, connection) = CreateContext();
        using var _ = connection;
        await service.SaveApplicantInfoAsync(app, ApplicantInput("Original", []));
        var loadedByBoth = app.ApplicantInfo!.RowVersion;

        var first = await service.SaveApplicantInfoAsync(app, ApplicantInput("Applicant A's edit", loadedByBoth));
        var second = await service.SaveApplicantInfoAsync(app, ApplicantInput("Applicant B's edit", loadedByBoth));

        first.Succeeded.Should().BeTrue();
        second.Succeeded.Should().BeFalse();
        var persisted = await db.ApplicantInfos.AsNoTracking().SingleAsync(i => i.ApplicationId == app.Id);
        persisted.FullName.Should().Be("Applicant A's edit");
    }

    [Fact]
    public async Task SaveApplicantInfo_RejectedSave_ReloadShowsTheOtherApplicantsData()
    {
        var (db, service, app, connection) = CreateContext();
        using var _ = connection;
        await service.SaveApplicantInfoAsync(app, ApplicantInput("Original", []));
        var loadedByBoth = app.ApplicantInfo!.RowVersion;
        await service.SaveApplicantInfoAsync(app, ApplicantInput("Applicant A's edit", loadedByBoth));

        var rejected = await service.SaveApplicantInfoAsync(app, ApplicantInput("Applicant B's edit", loadedByBoth));

        rejected.Succeeded.Should().BeFalse();
        rejected.Errors.Values.SelectMany(e => e).Should().ContainSingle(m => m.Contains("reload", StringComparison.OrdinalIgnoreCase));
        var reloaded = await db.ApplicantInfos.AsNoTracking().SingleAsync(i => i.ApplicationId == app.Id);
        reloaded.FullName.Should().Be("Applicant A's edit"); // not a merge, not B's rejected edit
    }

    [Fact]
    public async Task UpdateResidence_SecondSaveAgainstStaleRowVersion_IsRejectedAndDataSurvives()
    {
        var (db, service, app, connection) = CreateContext();
        using var _ = connection;
        var residence = await service.AddResidenceAsync(app, ResidenceInput("Original Landlord", []));
        var loadedByBoth = residence.RowVersion;

        var first = await service.UpdateResidenceAsync(residence, ResidenceInput("Applicant A's edit", loadedByBoth));
        var second = await service.UpdateResidenceAsync(residence, ResidenceInput("Applicant B's edit", loadedByBoth));

        first.Succeeded.Should().BeTrue();
        second.Succeeded.Should().BeFalse();
        var persisted = await db.Residences.AsNoTracking().SingleAsync(r => r.Id == residence.Id);
        persisted.LandlordName.Should().Be("Applicant A's edit");
    }

    [Fact]
    public async Task ConcurrentSaves_ToDifferentSections_BothPersist()
    {
        // MULTI-3: applicant A saves Applicant Information while applicant B saves a
        // Residence History row — different aggregates, different RowVersion tokens, so
        // neither save's WHERE clause can conflict with the other's.
        var (db, service, app, connection) = CreateContext();
        using var _ = connection;

        var infoResult = await service.SaveApplicantInfoAsync(app, ApplicantInput("Applicant A", []));
        var residence = await service.AddResidenceAsync(app, ResidenceInput("Applicant B's landlord", []));

        infoResult.Succeeded.Should().BeTrue();
        (await db.ApplicantInfos.AsNoTracking().SingleAsync(i => i.ApplicationId == app.Id)).FullName.Should().Be("Applicant A");
        (await db.Residences.AsNoTracking().SingleAsync(r => r.Id == residence.Id)).LandlordName.Should().Be("Applicant B's landlord");
    }

    private static async Task SeedUserAsync(AppDbContext db, string userId, string email, string displayName, string roleId, string roleName)
    {
        if (await db.Roles.FindAsync(roleId) is null)
        {
            db.Roles.Add(new IdentityRole { Id = roleId, Name = roleName, NormalizedName = roleName.ToUpperInvariant() });
        }

        db.Users.Add(new ApplicationUser { Id = userId, UserName = email, Email = email, NormalizedEmail = email.ToUpperInvariant(), DisplayName = displayName });
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = userId, RoleId = roleId });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AddApplicant_RegisteredApplicant_Succeeds()
    {
        var (db, service, app, connection) = CreateContext();
        using var _ = connection;
        await SeedUserAsync(db, "user1", "co@demo.local", "Co Applicant", "role-applicant", "Applicant");

        var result = await service.AddApplicantAsync(app, "co@demo.local");

        result.Succeeded.Should().BeTrue();
        db.ApplicationApplicants.Should().ContainSingle(a => a.ApplicationId == app.Id && a.UserId == "user1");
    }

    [Fact]
    public async Task AddApplicant_UnknownEmail_Fails()
    {
        var (db, service, app, connection) = CreateContext();
        using var _ = connection;

        var result = await service.AddApplicantAsync(app, "nobody@demo.local");

        result.Succeeded.Should().BeFalse();
        db.ApplicationApplicants.Should().BeEmpty();
    }

    [Fact]
    public async Task AddApplicant_PropertyManagerEmail_Fails()
    {
        var (db, service, app, connection) = CreateContext();
        using var _ = connection;
        await SeedUserAsync(db, "pm1", "pm@demo.local", "PM User", "role-pm", "PropertyManager");

        var result = await service.AddApplicantAsync(app, "pm@demo.local");

        result.Succeeded.Should().BeFalse();
        db.ApplicationApplicants.Should().BeEmpty();
    }

    [Fact]
    public async Task AddApplicant_AlreadyOnApplication_Fails()
    {
        var (db, service, app, connection) = CreateContext();
        using var _ = connection;
        await SeedUserAsync(db, "user1", "co@demo.local", "Co Applicant", "role-applicant", "Applicant");
        await service.AddApplicantAsync(app, "co@demo.local");

        var result = await service.AddApplicantAsync(app, "co@demo.local");

        result.Succeeded.Should().BeFalse();
        db.ApplicationApplicants.Where(a => a.ApplicationId == app.Id).Should().ContainSingle();
    }

    [Fact]
    public async Task AddApplicant_EmailIsCaseInsensitive()
    {
        var (db, service, app, connection) = CreateContext();
        using var _ = connection;
        await SeedUserAsync(db, "user1", "co@demo.local", "Co Applicant", "role-applicant", "Applicant");

        var result = await service.AddApplicantAsync(app, "CO@DEMO.LOCAL");

        result.Succeeded.Should().BeTrue();
    }
}
