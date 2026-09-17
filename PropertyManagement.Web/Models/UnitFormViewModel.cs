using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PropertyManagement.Web.Models;

public class UnitFormViewModel
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    [Required]
    [StringLength(20)]
    [Display(Name = "Unit number")]
    public string UnitNumber { get; set; } = string.Empty;

    [Range(0, 10)]
    public int Bedrooms { get; set; }

    [Range(0, 10)]
    public int Bathrooms { get; set; }

    [Range(0.01, 1_000_000)]
    [Display(Name = "Monthly rent")]
    public decimal MonthlyRent { get; set; }

    [Required(ErrorMessage = "Select a unit type.")]
    [Display(Name = "Unit type")]
    public int? UnitTypeId { get; set; }

    /// <summary>Populated by the controller for the dropdown; never bound from the POST body.</summary>
    [BindNever]
    public List<SelectListItem> UnitTypeOptions { get; set; } = [];
}
