using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Rules;
using PropertyManagement.Infrastructure.Data;
using PropertyManagement.Web.Models;

namespace PropertyManagement.Web.ViewComponents;

/// <summary>
/// Self-contained widget: given a property id, loads and renders that property's active
/// units with live availability — independent of whatever the host page already has in
/// hand. Reused both for the initial property list page and as the AJAX-refresh target
/// after any unit create/edit/remove (a controller action can return
/// <c>ViewComponent("UnitList", new { propertyId })</c> directly).
/// </summary>
public class UnitListViewComponent(AppDbContext db, TimeProvider timeProvider) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int propertyId)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var units = await db.Units
            .Where(u => u.IsActive && u.PropertyId == propertyId)
            .Include(u => u.UnitType)
            .OrderBy(u => u.UnitNumber)
            .AsNoTracking()
            .ToListAsync();

        var unitIds = units.Select(u => u.Id).ToList();
        var unavailableUnitIds = await db.Leases
            .Where(l => unitIds.Contains(l.UnitId))
            .CoveringDate(today)
            .Select(l => l.UnitId)
            .ToListAsync();

        var model = new UnitListViewModel
        {
            PropertyId = propertyId,
            Units = units.Select(u => new UnitRowViewModel
            {
                Id = u.Id,
                PropertyId = u.PropertyId,
                UnitNumber = u.UnitNumber,
                Bedrooms = u.Bedrooms,
                MonthlyRent = u.MonthlyRent,
                UnitTypeName = u.UnitType.Name,
                IsAvailable = !unavailableUnitIds.Contains(u.Id),
            }).ToList(),
        };

        return View(model);
    }
}
