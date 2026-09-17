using System.ComponentModel.DataAnnotations;

namespace PropertyManagement.Web.Models;

public class ApplicantInfoSectionViewModel
{
    /// <summary>WIZ-5: server-computed; same partial renders inputs vs plain text.
    /// Never trust a posted value for authorization — write guard is separate.</summary>
    public bool IsEditable { get; set; }

    /// <summary>MULTI-4: round-tripped from the loaded entity; checked as the EF
    /// original value on save so a concurrent edit is rejected, not overwritten.
    /// Empty for a section that has never been saved (nothing to conflict with).</summary>
    public byte[] RowVersion { get; set; } = [];

    [Required]
    [StringLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [Phone]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [Display(Name = "Address")]
    public string AddressLine1 { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Address line 2")]
    public string? AddressLine2 { get; set; }

    [Required]
    [StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string State { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    [Display(Name = "ZIP code")]
    public string ZipCode { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Date of birth")]
    [DataType(DataType.Date)]
    public DateOnly? DateOfBirth { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Employment")]
    public string? Employment { get; set; }

    [Required]
    [Range(0.01, 10_000_000)]
    [Display(Name = "Annual income")]
    [DataType(DataType.Currency)]
    public decimal? AnnualIncome { get; set; }

    [Required]
    [Display(Name = "Move-in date")]
    [DataType(DataType.Date)]
    public DateOnly? DesiredMoveInDate { get; set; }
}
