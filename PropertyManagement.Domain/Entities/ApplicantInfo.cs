namespace PropertyManagement.Domain.Entities;

/// <summary>
/// Wizard Section 1 (Features/04, WIZ-6) — one row per Application (1:1), with its own
/// concurrency token so it can be saved independently of Residence History (Features/14,
/// MULTI-3). Deliberately separate from the applicant's account profile: this captures
/// contact/address details as of the application, which may differ from the account.
/// </summary>
public class ApplicantInfo
{
    public int ApplicationId { get; set; }
    public Application Application { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }
    public string? Employment { get; set; }
    public decimal? AnnualIncome { get; set; }
    public DateOnly? DesiredMoveInDate { get; set; }

    /// <summary>Also the "section saved" completion marker used by the Summary gate
    /// (Features/04, WIZ-4) — non-null once this row exists.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
