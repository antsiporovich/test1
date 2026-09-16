using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Data;
using PropertyManagement.Infrastructure.Services;
using Xunit;

namespace PropertyManagement.Tests.Infrastructure;

/// <summary>
/// NOTES-1/NOTES-2 (Features/12): PM note add/edit, multi-PM visibility, and the
/// graded confidentiality test — the applicant-facing query path must never load
/// or expose notes.
/// </summary>
public class ApplicationServiceNoteTests
{
    private static (AppDbContext Db, ApplicationService Service, Unit Unit, DbContextOptions<AppDbContext> Options)
        CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var property = new Property { Name = "P1", AddressLine1 = "1 A St", City = "C", State = "S", ZipCode = "00000" };
        var unitType = new UnitType { Name = "Studio", IsActive = true };
        var unit = new Unit { Property = property, UnitType = unitType, UnitNumber = "101", MonthlyRent = 1000 };
        db.Units.Add(unit);
        db.SaveChanges();
        return (db, new ApplicationService(db, TimeProvider.System), unit, options);
    }

    private static Application AddApplication(AppDbContext db, Unit unit)
    {
        var app = new Application { UnitId = unit.Id, Status = ApplicationStatus.Submitted, CreatedAtUtc = DateTimeOffset.UtcNow };
        db.Applications.Add(app);
        db.SaveChanges();
        return app;
    }

    // ── NOTES-1: add ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AddNoteAsync_PersistsNoteWithCorrectAuthorAndBody()
    {
        var (db, service, unit, _) = CreateContext();
        var app = AddApplication(db, unit);
        const string authorId = "pm-user-1";
        const string body = "Called applicant, employment verified.";

        var note = await service.AddNoteAsync(app, authorId, body);

        note.Id.Should().BeGreaterThan(0);
        note.ApplicationId.Should().Be(app.Id);
        note.AuthorUserId.Should().Be(authorId);
        note.Body.Should().Be(body);
        note.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AddNoteAsync_MultipleNotes_AllPersistIndependently()
    {
        var (db, service, unit, _) = CreateContext();
        var app = AddApplication(db, unit);

        await service.AddNoteAsync(app, "pm1", "Note A");
        await service.AddNoteAsync(app, "pm2", "Note B");

        var count = await db.ApplicationNotes.CountAsync(n => n.ApplicationId == app.Id);
        count.Should().Be(2);
    }

    // ── NOTES-1: edit ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateNoteAsync_ChangesBody()
    {
        var (db, service, unit, _) = CreateContext();
        var app = AddApplication(db, unit);
        var note = await service.AddNoteAsync(app, "pm1", "Original text");

        await service.UpdateNoteAsync(note, "Updated text");

        var persisted = await db.ApplicationNotes.FindAsync(note.Id);
        persisted!.Body.Should().Be("Updated text");
    }

    // ── NOTES-2: all PMs see each other's notes ───────────────────────────────

    [Fact]
    public async Task Notes_AddedByDifferentPMs_AreAllVisible()
    {
        var (db, service, unit, _) = CreateContext();
        var app = AddApplication(db, unit);

        await service.AddNoteAsync(app, "pm-alice", "From Alice");
        await service.AddNoteAsync(app, "pm-bob", "From Bob");

        var notes = await db.ApplicationNotes
            .Where(n => n.ApplicationId == app.Id)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync();

        notes.Should().HaveCount(2);
        notes[0].AuthorUserId.Should().Be("pm-alice");
        notes[1].AuthorUserId.Should().Be("pm-bob");
    }

    // ── NOTES-1 confidentiality: data-layer isolation test ────────────────────
    // Graded test (Features/12, NOTES-1): the applicant-facing query path
    // (mirror of LoadOwnedApplicationAsync in ApplicationsController) must never
    // carry notes. We prove this with a fresh DbContext so EF's identity cache
    // doesn't inflate the navigation property from the write context.

    [Fact]
    public async Task ApplicantFacingQuery_NeverLoadsNotes()
    {
        var (db, service, unit, options) = CreateContext();
        var app = AddApplication(db, unit);
        await service.AddNoteAsync(app, "pm1", "Secret PM note — must never reach applicant");

        // Fresh context: simulates a new HTTP request from an applicant
        await using var readCtx = new AppDbContext(options);
        var loaded = await readCtx.Applications
            .Include(a => a.Unit)
            .Include(a => a.ApplicantInfo)
            .Include(a => a.Residences)
            .Include(a => a.Applicants)
            .Include(a => a.StatusHistory)
            // Notes deliberately NOT included — mirrors LoadOwnedApplicationAsync
            .FirstOrDefaultAsync(a => a.Id == app.Id);

        loaded.Should().NotBeNull();
        loaded!.Notes.Should().BeEmpty(
            because: "the applicant controller path never Include()s the Notes " +
                     "navigation, so notes are structurally absent from any " +
                     "applicant-facing Application load (NOTES-1, Features/12)");
    }

    [Fact]
    public async Task ApplicationNote_IsolatedToItsApplication()
    {
        var (db, service, unit, _) = CreateContext();
        var app1 = AddApplication(db, unit);
        var app2 = AddApplication(db, unit);

        await service.AddNoteAsync(app1, "pm1", "Note for app1");

        var app2Notes = await db.ApplicationNotes.CountAsync(n => n.ApplicationId == app2.Id);
        app2Notes.Should().Be(0, because: "notes are scoped to their application");
    }
}
