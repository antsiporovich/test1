using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities;

/// <summary>
/// One row per transition (submit, return, approve, deny, withdraw, claim/release),
/// written at every transition point rather than reconstructed after the fact
/// (Features/07, REVIEW technical notes).
/// </summary>
public class ApplicationStatusHistory
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }
    public Application Application { get; set; } = null!;

    public ApplicationStatus? FromStatus { get; set; }
    public ApplicationStatus ToStatus { get; set; }

    public string ActorUserId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string? Comment { get; set; }
}
