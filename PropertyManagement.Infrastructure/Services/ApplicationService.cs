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
