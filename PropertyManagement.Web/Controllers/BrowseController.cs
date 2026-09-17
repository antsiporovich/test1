using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Infrastructure.Identity;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Web.Models;

namespace PropertyManagement.Web.Controllers;

[Authorize]
[Route("Browse")]
public class BrowseController(IUnitBrowseService browseService, UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Browsing-to-apply is Applicant-only; a PM here is sent to their own management view.
        if (User.IsInRole("PropertyManager"))
        {
            return RedirectToAction(nameof(PropertiesController.Index), "Properties");
        }

        var userId = userManager.GetUserId(User);
        var units = await browseService.GetAvailableUnitsAsync(userId, ct);
        var vm = units.Select(u => new AvailableUnitRowViewModel
        {
            UnitId = u.Id,
            PropertyName = u.Property.Name,
            PropertyAddress = $"{u.Property.AddressLine1}, {u.Property.City}",
            UnitNumber = u.UnitNumber,
            Bedrooms = u.Bedrooms,
            MonthlyRent = u.MonthlyRent,
            UnitTypeName = u.UnitType.Name,
        }).ToList();

        return View(vm);
    }

    [HttpPost("Apply/{unitId:int}")]
    [Authorize(Roles = "Applicant")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(int unitId, CancellationToken ct)
    {
        var userId = userManager.GetUserId(User)!;
        var result = await browseService.ApplyAsync(unitId, userId, ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Errors.Values.SelectMany(e => e).FirstOrDefault();
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(ApplicationsController.Wizard), "Applications", new { id = result.Value!.Id });
    }
}
