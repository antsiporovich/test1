using System.ComponentModel.DataAnnotations;

namespace PropertyManagement.Web.Models;

public class ResidenceFormViewModel : IValidatableObject
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }

    /// <summary>MULTI-4: round-tripped from the loaded entity; checked as the EF
    /// original value on save so a concurrent edit is rejected, not overwritten.</summary>
    public byte[] RowVersion { get; set; } = [];

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
    [StringLength(200)]
    [Display(Name = "Landlord name")]
    public string LandlordName { get; set; } = string.Empty;

    [Required]
    [Phone]
    [Display(Name = "Landlord phone")]
    public string LandlordPhone { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Move-in date")]
    [DataType(DataType.Date)]
    public DateOnly MoveInDate { get; set; }

    [Display(Name = "Move-out date")]
    [DataType(DataType.Date)]
    public DateOnly? MoveOutDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MoveOutDate is not null && MoveOutDate < MoveInDate)
        {
            yield return new ValidationResult(
                "Move-out date cannot be before the move-in date.",
                [nameof(MoveOutDate)]);
        }
    }
}
