using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class ApplicationService(AppDbContext db, TimeProvider timeProvider) : IApplicationService
{
    public async Task SaveApplicantInfoAsync(Application application, ApplicantInfoInput input, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        if (application.ApplicantInfo is null)
        {
            application.ApplicantInfo = new ApplicantInfo { ApplicationId = application.Id };
            db.ApplicantInfos.Add(application.ApplicantInfo);
        }

        var info = application.ApplicantInfo;
        info.FullName = input.FullName;
        info.Phone = input.Phone;
        info.Email = input.Email;
        info.AddressLine1 = input.AddressLine1;
        info.AddressLine2 = input.AddressLine2;
        info.City = input.City;
        info.State = input.State;
        info.ZipCode = input.ZipCode;
        info.UpdatedAtUtc = now;

        await db.SaveChangesAsync(ct);
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

    public async Task UpdateResidenceAsync(Residence residence, ResidenceInput input, CancellationToken ct = default)
    {
        ApplyResidenceInput(residence, input);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveResidenceAsync(Residence residence, CancellationToken ct = default)
    {
        db.Residences.Remove(residence);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ServiceResult<bool>> SubmitAsync(Application application, string actorUserId, CancellationToken ct = default)
    {
        if (!WizardStepRules.BothSectionsSaved(application))
        {
            return ServiceResult<bool>.Fail(string.Empty, "Complete both sections before submitting.");
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
    }
}
