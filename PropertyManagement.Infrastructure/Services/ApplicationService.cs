using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;
using PropertyManagement.Domain.Validation;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class ApplicationService(AppDbContext db, TimeProvider timeProvider) : IApplicationService
{
    public async Task<ServiceResult<bool>> SaveApplicantInfoAsync(Application application, ApplicantInfoInput input, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var isNew = application.ApplicantInfo is null;
        if (isNew)
        {
            application.ApplicantInfo = new ApplicantInfo { ApplicationId = application.Id };
            db.ApplicantInfos.Add(application.ApplicantInfo);
        }

        var info = application.ApplicantInfo!;
        info.FullName = input.FullName;
        info.Phone = input.Phone;
        info.Email = input.Email;
        info.AddressLine1 = input.AddressLine1;
        info.AddressLine2 = input.AddressLine2;
        info.City = input.City;
        info.State = input.State;
        info.ZipCode = input.ZipCode;
        info.DateOfBirth = input.DateOfBirth;
        info.Employment = input.Employment;
        info.AnnualIncome = input.AnnualIncome;
        info.DesiredMoveInDate = input.DesiredMoveInDate;
        info.UpdatedAtUtc = now;

        // MULTI-4: a brand-new row has no prior version to conflict with; only an
        // existing row's WHERE clause needs the loaded RowVersion as the original value.
        if (!isNew)
        {
            db.Entry(info).Property(e => e.RowVersion).OriginalValue = input.RowVersion;
        }

        return await SaveWithConcurrencyCheckAsync(ct);
    }

    public async Task ConfirmResidenceHistoryAsync(Application application, CancellationToken ct = default)
    {
        application.ResidenceHistoryConfirmedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<Residence> AddResidenceAsync(Application application, ResidenceInput input, CancellationToken ct = default)
    {
        var residence = new Residence { ApplicationId = application.Id };
        ApplyResidenceInput(residence, input);
        db.Residences.Add(residence);
        await db.SaveChangesAsync(ct);
        return residence;
    }

    public async Task<ServiceResult<bool>> UpdateResidenceAsync(Residence residence, ResidenceInput input, CancellationToken ct = default)
    {
        ApplyResidenceInput(residence, input);
        db.Entry(residence).Property(e => e.RowVersion).OriginalValue = input.RowVersion;
        return await SaveWithConcurrencyCheckAsync(ct);
    }

    public async Task RemoveResidenceAsync(Residence residence, CancellationToken ct = default)
    {
        db.Residences.Remove(residence);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ServiceResult<bool>> AddApplicantAsync(Application application, string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        var isApplicant = user is not null && await (
            from ur in db.UserRoles
            join r in db.Roles on ur.RoleId equals r.Id
            where ur.UserId == user.Id && r.Name == "Applicant"
            select ur).AnyAsync(ct);

        if (user is null || !isApplicant)
        {
            return ServiceResult<bool>.Fail("Email", "No Applicant account found with that email.");
        }

        if (application.Applicants.Any(a => a.UserId == user.Id))
        {
            return ServiceResult<bool>.Fail("Email", "This person is already on the application.");
        }

        application.Applicants.Add(new ApplicationApplicant
        {
            ApplicationId = application.Id,
            UserId = user.Id,
            AddedAtUtc = timeProvider.GetUtcNow(),
        });

        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> SubmitAsync(Application application, string actorUserId, CancellationToken ct = default)
    {
        // VALID-3: re-runs the same shared validators the Summary lists errors from —
        // never trusts a client-reported "all clear" state.
        if (ApplicationValidation.GetOutstandingErrors(application).Any())
        {
            return ServiceResult<bool>.Fail(string.Empty, "Resolve the outstanding issues below before submitting.");
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var hasActiveLease = await db.Leases.Where(l => l.UnitId == application.UnitId).CoveringDate(today).AnyAsync(ct);
        if (hasActiveLease)
        {
            return ServiceResult<bool>.Fail(string.Empty, "This unit is no longer available.");
        }

        var now = timeProvider.GetUtcNow();
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            FromStatus = application.Status,
            ToStatus = ApplicationStatus.Submitted,
            ActorUserId = actorUserId,
            Timestamp = now,
        });
        application.Status = ApplicationStatus.Submitted;
        application.SubmittedAtUtc = now;

        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> WithdrawAsync(Application application, string actorUserId, CancellationToken ct = default)
    {
        if (application.Status.IsTerminal())
        {
            return ServiceResult<bool>.Fail(string.Empty, "This application can no longer be withdrawn.");
        }

        var now = timeProvider.GetUtcNow();
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            FromStatus = application.Status,
            ToStatus = ApplicationStatus.Withdrawn,
            ActorUserId = actorUserId,
            Timestamp = now,
        });
        application.Status = ApplicationStatus.Withdrawn;

        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> ClaimAsync(int applicationId, string actorUserId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var now = timeProvider.GetUtcNow();

        // Single atomic conditional update — only succeeds if the row is still
        // Submitted at the moment this runs, so two PMs claiming at once can't both
        // win (QUEUE-1). ExecuteUpdateAsync issues one UPDATE ... WHERE, no
        // load-then-save race window.
        var rowsAffected = await db.Applications
            .Where(a => a.Id == applicationId && a.Status == ApplicationStatus.Submitted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Status, ApplicationStatus.UnderReview)
                .SetProperty(a => a.ClaimedByUserId, actorUserId)
                .SetProperty(a => a.ClaimedAtUtc, now), ct);

        if (rowsAffected == 0)
        {
            await transaction.RollbackAsync(ct);
            var current = await db.Applications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == applicationId, ct);
            return current switch
            {
                null => ServiceResult<bool>.Fail(string.Empty, "Application not found."),
                { Status: ApplicationStatus.UnderReview } => ServiceResult<bool>.Fail(string.Empty, "This application is already claimed by another Property Manager."),
                _ => ServiceResult<bool>.Fail(string.Empty, $"This application can no longer be claimed (status: {current.Status})."),
            };
        }

        db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            ApplicationId = applicationId,
            FromStatus = ApplicationStatus.Submitted,
            ToStatus = ApplicationStatus.UnderReview,
            ActorUserId = actorUserId,
            Timestamp = now,
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> ReleaseAsync(int applicationId, string actorUserId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var rowsAffected = await db.Applications
            .Where(a => a.Id == applicationId && a.Status == ApplicationStatus.UnderReview && a.ClaimedByUserId == actorUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Status, ApplicationStatus.Submitted)
                .SetProperty(a => a.ClaimedByUserId, (string?)null)
                .SetProperty(a => a.ClaimedAtUtc, (DateTimeOffset?)null), ct);

        if (rowsAffected == 0)
        {
            await transaction.RollbackAsync(ct);
            return ServiceResult<bool>.Fail(string.Empty, "You can only release an application you claimed.");
        }

        db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            ApplicationId = applicationId,
            FromStatus = ApplicationStatus.UnderReview,
            ToStatus = ApplicationStatus.Submitted,
            ActorUserId = actorUserId,
            Timestamp = timeProvider.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> ApproveAsync(Application application, string actorUserId, string? comment, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // REVIEW-2: same active-lease predicate as LIFE-1's submit guard (two call sites,
        // one method — LeaseAvailabilityRules.CoveringDate). Prevents a second lease
        // if another application for the same unit was approved between submission and now.
        var hasActiveLease = await db.Leases
            .Where(l => l.UnitId == application.UnitId)
            .CoveringDate(today)
            .AnyAsync(ct);

        if (hasActiveLease)
        {
            return ServiceResult<bool>.Fail(string.Empty, "This unit already has an active lease. Approval is not possible.");
        }

        var now = timeProvider.GetUtcNow();

        // LEASE-1: 12-month lease via the domain factory (StartDate = today, clock-free testable).
        db.Leases.Add(Lease.Create(application.UnitId, application.Id, today, now));

        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            FromStatus = application.Status,
            ToStatus = ApplicationStatus.Approved,
            ActorUserId = actorUserId,
            Timestamp = now,
            Comment = comment,
        });
        application.Status = ApplicationStatus.Approved;

        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> ReturnToApplicantAsync(Application application, string actorUserId, string comment, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            FromStatus = application.Status,
            ToStatus = ApplicationStatus.Returned,
            ActorUserId = actorUserId,
            Timestamp = now,
            Comment = comment,
        });
        application.Status = ApplicationStatus.Returned;
        // Clear claim if previously Under Review (QUEUE-1 path).
        application.ClaimedByUserId = null;
        application.ClaimedAtUtc = null;

        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> DenyAsync(Application application, string actorUserId, string comment, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            FromStatus = application.Status,
            ToStatus = ApplicationStatus.Denied,
            ActorUserId = actorUserId,
            Timestamp = now,
            Comment = comment,
        });
        application.Status = ApplicationStatus.Denied;
        // Clear claim if previously Under Review.
        application.ClaimedByUserId = null;
        application.ClaimedAtUtc = null;

        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private const string ConcurrencyErrorMessage = "This section was changed since you loaded it — please reload and try again.";

    private async Task<ServiceResult<bool>> SaveWithConcurrencyCheckAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return ServiceResult<bool>.Success(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<bool>.Fail(string.Empty, ConcurrencyErrorMessage);
        }
    }

    // ── NOTES-1 ───────────────────────────────────────────────────────────────

    public async Task<ApplicationNote> AddNoteAsync(Application application, string authorUserId, string body, CancellationToken ct = default)
    {
        var note = new ApplicationNote
        {
            ApplicationId = application.Id,
            AuthorUserId = authorUserId,
            Body = body,
            CreatedAt = timeProvider.GetUtcNow(),
        };
        db.ApplicationNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return note;
    }

    public async Task UpdateNoteAsync(ApplicationNote note, string body, CancellationToken ct = default)
    {
        note.Body = body;
        await db.SaveChangesAsync(ct);
    }

    private static void ApplyResidenceInput(Residence residence, ResidenceInput input)
    {
        residence.AddressLine1 = input.AddressLine1;
        residence.AddressLine2 = input.AddressLine2;
        residence.City = input.City;
        residence.State = input.State;
        residence.ZipCode = input.ZipCode;
        residence.LandlordName = input.LandlordName;
        residence.LandlordPhone = input.LandlordPhone;
        residence.MoveInDate = input.MoveInDate;
        residence.MoveOutDate = input.MoveOutDate;
        residence.MonthlyRent = input.MonthlyRent;
        residence.ReasonForLeaving = input.ReasonForLeaving;
    }
}
