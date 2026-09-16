using System.ComponentModel.DataAnnotations;

namespace PropertyManagement.Web.Models;

public class ApplicantInfoSectionViewModel
{
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
}
