using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities;

/// <summary>
/// Aggregate root for a rental application. Section data (ApplicantInfo, Residences)
/// is persisted in its own child table with its own concurrency token, rather than as
/// fields on this row, so that concurrent edits to different sections never collide on
/// the same RowVersion (Features/14, MULTI-3/MULTI-4).
/// </summary>
public class Application
{
    public int Id { get; set; }

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? SubmittedAtUtc { get; set; }

    /// <summary>Non-null once Continue has been clicked on Residence History at least
    /// once (zero residences is valid, so presence of rows can't signal this — Features/04,
    /// WIZ-7). Applicant Information's equivalent marker is ApplicantInfo.UpdatedAtUtc,
    /// since that section always has its own row once saved.</summary>
    public DateTimeOffset? ResidenceHistoryConfirmedAtUtc { get; set; }

    /// <summary>Bonus 2 review queue claim (Features/11, QUEUE-1/QUEUE-2).</summary>
    public string? ClaimedByUserId { get; set; }
    public DateTimeOffset? ClaimedAtUtc { get; set; }

    /// <summary>Guards status transitions and claim/release against races; each section's
    /// own edits use their own token instead (see class remarks).</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ApplicantInfo? ApplicantInfo { get; set; }
    public Lease? Lease { get; set; }

    public ICollection<ApplicationApplicant> Applicants { get; set; } = new List<ApplicationApplicant>();
    public ICollection<Residence> Residences { get; set; } = new List<Residence>();
    public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = new List<ApplicationStatusHistory>();
    public ICollection<ApplicationNote> Notes { get; set; } = new List<ApplicationNote>();
}
