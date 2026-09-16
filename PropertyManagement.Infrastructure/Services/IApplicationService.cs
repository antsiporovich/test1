using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Services;

public record ApplicantInfoInput(string FullName, string Phone, string Email, string AddressLine1, string? AddressLine2, string City, string State, string ZipCode, byte[] RowVersion);

public record ResidenceInput(string AddressLine1, string? AddressLine2, string City, string State, string ZipCode, string LandlordName, string LandlordPhone, DateOnly MoveInDate, DateOnly? MoveOutDate, byte[] RowVersion);

/// <summary>
/// All operations take an already-loaded, ownership-checked <see cref="Application"/>
/// (and, for residences, an already-resolved <see cref="Residence"/> under it) — the
/// controller does the "find + authorize" (per aspnet-identity-authorization.md's
/// "simple approach"), this service does the mutation. Residence-create and remove have
/// no DB-dependent failure mode once the caller is authorized, so they act directly;
/// every other mutation below carries a real failure mode (concurrency conflict or
/// business-rule rejection) and returns a <see cref="ServiceResult{T}"/>.
/// </summary>
public interface IApplicationService
{
    /// <summary>MULTI-4: <paramref name="input"/>'s RowVersion is checked as the EF
    /// original value; a concurrent save since it was loaded rejects this one rather
    /// than overwriting it (never last-write-wins).</summary>
    Task<ServiceResult<bool>> SaveApplicantInfoAsync(Application application, ApplicantInfoInput input, CancellationToken ct = default);

    Task ConfirmResidenceHistoryAsync(Application application, CancellationToken ct = default);

    Task<Residence> AddResidenceAsync(Application application, ResidenceInput input, CancellationToken ct = default);

    /// <summary>MULTI-4: same concurrency check as <see cref="SaveApplicantInfoAsync"/>.</summary>
    Task<ServiceResult<bool>> UpdateResidenceAsync(Residence residence, ResidenceInput input, CancellationToken ct = default);

    Task RemoveResidenceAsync(Residence residence, CancellationToken ct = default);

    /// <summary>MULTI-1: adds a registered Applicant (matched by email, case-insensitive)
    /// to the application's applicant set. Fails if no such Applicant account exists or
    /// they're already on this application; the unique (ApplicationId, UserId) index is
    /// the DB-level backstop.</summary>
    Task<ServiceResult<bool>> AddApplicantAsync(Application application, string email, CancellationToken ct = default);

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

    /// <summary>REVIEW-2: active-lease guard (same predicate as LIFE-1 submit check,
    /// two call sites one method — Features/07). Creates a 12-month Lease via
    /// <see cref="Lease.Create"/>; transitions to Approved (terminal); records history.</summary>
    Task<ServiceResult<bool>> ApproveAsync(Application application, string actorUserId, string? comment, CancellationToken ct = default);

    /// <summary>REVIEW-3: transitions to Returned (editable); records history with
    /// required comment so applicant can see it and correct (LIFE-3).</summary>
    Task<ServiceResult<bool>> ReturnToApplicantAsync(Application application, string actorUserId, string comment, CancellationToken ct = default);

    /// <summary>REVIEW-4: transitions to Denied (terminal); records history with
    /// required comment.</summary>
    Task<ServiceResult<bool>> DenyAsync(Application application, string actorUserId, string comment, CancellationToken ct = default);

    /// <summary>NOTES-1: appends a new timestamped + attributed note. PM-only; the
    /// controller is responsible for the role check before calling this.</summary>
    Task<ApplicationNote> AddNoteAsync(Application application, string authorUserId, string body, CancellationToken ct = default);

    /// <summary>NOTES-1: overwrites the body of an existing note.</summary>
    Task UpdateNoteAsync(ApplicationNote note, string body, CancellationToken ct = default);
}
