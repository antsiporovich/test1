using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Services;

public record ApplicantInfoInput(string FullName, string Phone, string Email, string AddressLine1, string? AddressLine2, string City, string State, string ZipCode);

public record ResidenceInput(string AddressLine1, string? AddressLine2, string City, string State, string ZipCode, string LandlordName, string LandlordPhone, DateOnly MoveInDate, DateOnly? MoveOutDate);

/// <summary>
/// All operations take an already-loaded, ownership-checked <see cref="Application"/>
/// (and, for residences, an already-resolved <see cref="Residence"/> under it) — the
/// controller does the "find + authorize" (per aspnet-identity-authorization.md's
/// "simple approach"), this service does the mutation. Section-save and residence-CRUD
/// methods have no DB-dependent failure mode once the caller is authorized, so they
/// return the mutated entity directly rather than a ServiceResult; Submit/Withdraw carry
/// real business-rule failures and return ServiceResult.
/// </summary>
public interface IApplicationService
{
    Task SaveApplicantInfoAsync(Application application, ApplicantInfoInput input, CancellationToken ct = default);

    Task ConfirmResidenceHistoryAsync(Application application, CancellationToken ct = default);

    Task<Residence> AddResidenceAsync(Application application, ResidenceInput input, CancellationToken ct = default);

    Task UpdateResidenceAsync(Residence residence, ResidenceInput input, CancellationToken ct = default);

    Task RemoveResidenceAsync(Residence residence, CancellationToken ct = default);

    /// <summary>LIFE-1: gated on no outstanding validation issues (Features/13, VALID-3)
    /// and no active lease on the unit.</summary>
    Task<ServiceResult<bool>> SubmitAsync(Application application, string actorUserId, CancellationToken ct = default);

    /// <summary>LIFE-2: gated on the application not already being terminal.</summary>
    Task<ServiceResult<bool>> WithdrawAsync(Application application, string actorUserId, CancellationToken ct = default);

    /// <summary>QUEUE-1: takes an id, not a loaded entity — the atomic conditional
    /// update (only succeeds if still Submitted) needs a fresh DB read at execution
    /// time, not a snapshot that could already be stale by the time this runs. Any PM
    /// may attempt this; there's no per-application ownership on the PM side.</summary>
    Task<ServiceResult<bool>> ClaimAsync(int applicationId, string actorUserId, CancellationToken ct = default);

    /// <summary>QUEUE-2: same atomic-update shape as Claim; the *primary* claimant-only
    /// check belongs in the controller (so a mismatch is a clean 403), this is the
    /// service-level backstop against the same id being released mid-flight.</summary>
    Task<ServiceResult<bool>> ReleaseAsync(int applicationId, string actorUserId, CancellationToken ct = default);
}
