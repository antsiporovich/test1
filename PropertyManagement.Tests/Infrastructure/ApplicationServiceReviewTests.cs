using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;
using PropertyManagement.Infrastructure.Data;
using PropertyManagement.Infrastructure.Services;
using Xunit;

namespace PropertyManagement.Tests.Infrastructure;

/// <summary>
/// REVIEW-2/REVIEW-3/REVIEW-4 service-layer tests (Features/07).
/// Uses InMemory provider — no transactions or ExecuteUpdateAsync needed here
/// (unlike claim/release which use ExecuteUpdateAsync).
/// </summary>
public class ApplicationServiceReviewTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    private static (AppDbContext Db, ApplicationService Service, Unit Unit) CreateContext()
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
        return (db, new ApplicationService(db, TimeProvider.System), unit);
    }

    private static Application AddApplication(AppDbContext db, Unit unit, ApplicationStatus status)
    {
        var app = new Application { UnitId = unit.Id, Status = status, CreatedAtUtc = DateTimeOffset.UtcNow };
        db.Applications.Add(app);
        db.SaveChanges();
        return app;
    }

    // ── REVIEW-2: Approve ─────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_NoActiveLease_TransitionsToApprovedAndCreatesLease()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Submitted);

        var result = await service.ApproveAsync(app, "pmA", comment: null);

        result.Succeeded.Should().BeTrue();
        app.Status.Should().Be(ApplicationStatus.Approved);

        var lease = await db.Leases.FirstOrDefaultAsync(l => l.ApplicationId == app.Id);
        lease.Should().NotBeNull();
        lease!.UnitId.Should().Be(unit.Id);
        lease.EndDate.Should().Be(lease.StartDate.AddMonths(12)); // LEASE-1
    }

    [Fact]
    public async Task Approve_LeaseTerm_IsExactlyTwelveMonths()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Submitted);

        await service.ApproveAsync(app, "pmA", comment: null);

        var lease = await db.Leases.FirstAsync(l => l.ApplicationId == app.Id);
        (lease.EndDate.Year * 12 + lease.EndDate.Month - (lease.StartDate.Year * 12 + lease.StartDate.Month))
            .Should().Be(12);
    }

    [Fact]
    public async Task Approve_RecordsHistoryRow()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Submitted);

        await service.ApproveAsync(app, "pmA", comment: "Looks good");

        db.ApplicationStatusHistories.Should().ContainSingle(h =>
            h.ApplicationId == app.Id &&
            h.ToStatus == ApplicationStatus.Approved &&
            h.ActorUserId == "pmA" &&
            h.Comment == "Looks good");
    }

    [Fact]
    public async Task Approve_UnitAlreadyHasActiveLease_RejectedNothingChanges()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Submitted);
        // Seed an active lease on the same unit (simulates another app being approved first).
        db.Leases.Add(new Lease { UnitId = unit.Id, ApplicationId = 999, StartDate = Today.AddDays(-1), EndDate = Today.AddMonths(12) });
        db.SaveChanges();

        var result = await service.ApproveAsync(app, "pmA", comment: null);

        result.Succeeded.Should().BeFalse();
        app.Status.Should().Be(ApplicationStatus.Submitted); // unchanged
        db.Leases.Count(l => l.ApplicationId == app.Id).Should().Be(0); // no second lease
    }

    [Fact]
    public async Task Approve_SiblingApplicationForSameUnit_IsUntouched()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Submitted);
        var sibling = AddApplication(db, unit, ApplicationStatus.Submitted);

        await service.ApproveAsync(app, "pmA", comment: null);

        sibling.Status.Should().Be(ApplicationStatus.Submitted); // spec: left as-is
    }

    // ── REVIEW-3: Return ──────────────────────────────────────────────────────

    [Fact]
    public async Task ReturnToApplicant_TransitionsToReturnedWithComment()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Submitted);

        var result = await service.ReturnToApplicantAsync(app, "pmA", "Please fix your address.");

        result.Succeeded.Should().BeTrue();
        app.Status.Should().Be(ApplicationStatus.Returned);
        db.ApplicationStatusHistories.Should().ContainSingle(h =>
            h.ToStatus == ApplicationStatus.Returned &&
            h.Comment == "Please fix your address." &&
            h.ActorUserId == "pmA");
    }

    [Fact]
    public async Task ReturnToApplicant_ClearsClaimFields()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.UnderReview);
        app.ClaimedByUserId = "pmA";
        app.ClaimedAtUtc = DateTimeOffset.UtcNow;
        db.SaveChanges();

        await service.ReturnToApplicantAsync(app, "pmA", "Needs corrections.");

        app.ClaimedByUserId.Should().BeNull();
        app.ClaimedAtUtc.Should().BeNull();
    }

    // ── REVIEW-4: Deny ────────────────────────────────────────────────────────

    [Fact]
    public async Task Deny_TransitionsToDeniedWithComment()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Submitted);

        var result = await service.DenyAsync(app, "pmA", "Application does not meet requirements.");

        result.Succeeded.Should().BeTrue();
        app.Status.Should().Be(ApplicationStatus.Denied);
        db.ApplicationStatusHistories.Should().ContainSingle(h =>
            h.ToStatus == ApplicationStatus.Denied &&
            h.Comment == "Application does not meet requirements.");
    }

    [Fact]
    public async Task Deny_IsTerminal_StatusDoesNotChangeAgain()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.Denied);

        // A denied application presented to Withdraw (terminal guard)
        var withdrawResult = await service.WithdrawAsync(app, "pmA");

        withdrawResult.Succeeded.Should().BeFalse();
        app.Status.Should().Be(ApplicationStatus.Denied);
    }

    [Fact]
    public async Task Deny_ClearsClaimFields()
    {
        var (db, service, unit) = CreateContext();
        var app = AddApplication(db, unit, ApplicationStatus.UnderReview);
        app.ClaimedByUserId = "pmA";
        app.ClaimedAtUtc = DateTimeOffset.UtcNow;
        db.SaveChanges();

        await service.DenyAsync(app, "pmA", "Denied.");

        app.ClaimedByUserId.Should().BeNull();
        app.ClaimedAtUtc.Should().BeNull();
    }

    // ── ReviewRules — comment-required rule (REVIEW-1) ───────────────────────
    // The "comment required" rule is in ReviewRules (Domain), shared by the Web
    // ViewModel's IValidatableObject and these tests — one definition, two consumers.

    [Theory]
    [InlineData(ReviewOutcome.Return)]
    [InlineData(ReviewOutcome.Deny)]
    public void ReviewRules_CommentRequired_ReturnOrDeny_IsTrue(ReviewOutcome outcome)
    {
        ReviewRules.CommentRequired(outcome).Should().BeTrue();
    }

    [Fact]
    public void ReviewRules_CommentRequired_Approve_IsFalse()
    {
        ReviewRules.CommentRequired(ReviewOutcome.Approve).Should().BeFalse();
    }
}
