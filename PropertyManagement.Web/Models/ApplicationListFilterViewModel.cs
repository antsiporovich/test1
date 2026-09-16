using Microsoft.AspNetCore.Mvc.Rendering;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Web.Models;

public class ApplicationListFilterViewModel
{
    public ApplicationStatus? Status { get; set; }
    public int? PropertyId { get; set; }

    public List<SelectListItem> StatusOptions { get; set; } = [];
    public List<SelectListItem> PropertyOptions { get; set; } = [];
    public List<ApplicationListRowViewModel> Rows { get; set; } = [];
}
