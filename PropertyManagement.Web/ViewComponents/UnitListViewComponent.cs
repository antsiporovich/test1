using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Web.Models;

namespace PropertyManagement.Web.ViewComponents;

/// <summary>
/// Self-contained widget: given a property id, loads and renders that property's active
/// units with live availability — independent of whatever the host page already has in
/// hand. Reused both for the initial property list page and as the AJAX-refresh target
/// after any unit create/edit/remove (a controller action can return
/// <c>ViewComponent("UnitList", new { propertyId })</c> directly).
/// </summary>
public class UnitListViewComponent(IUnitService unitService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int propertyId)
    {
        var rows = await unitService.GetActiveWithAvailabilityAsync(propertyId);

        var model = new UnitListViewModel
        {
            PropertyId = propertyId,
            Units = rows.Select(r => new UnitRowViewModel
            {
                Id = r.Unit.Id,
                PropertyId = r.Unit.PropertyId,
                UnitNumber = r.Unit.UnitNumber,
                Bedrooms = r.Unit.Bedrooms,
                MonthlyRent = r.Unit.MonthlyRent,
                UnitTypeName = r.Unit.UnitType.Name,
                IsAvailable = r.IsAvailable,
            }).ToList(),
        };

        return View(model);
    }
}
