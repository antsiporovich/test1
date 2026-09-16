using Microsoft.AspNetCore.Mvc.Rendering;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Web.Models;

public class ApplicationListFilterViewModel
{
    public ApplicationStatus? Status { get; set; }
    public int? PropertyId { get; set; }

    public List<SelectListItem> StatusOptions { get; set; } = [];
    public List<SelectListItem> PropertyOptions { get; set; } = [];

    /// <summary>PM-only (Bonus 2 review queue); null for Applicants.</summary>
    public List<ReviewQueueRowViewModel>? QueueRows { get; set; }
}
