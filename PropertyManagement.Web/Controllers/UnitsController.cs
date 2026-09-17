using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Web.Extensions;
using PropertyManagement.Web.Models;

namespace PropertyManagement.Web.Controllers;

[Authorize(Roles = "PropertyManager")]
[Route("Properties/{propertyId:int}/Units")]
public class UnitsController(IUnitService unitService) : Controller
{
    [HttpGet("Create")]
    public async Task<IActionResult> CreateForm(int propertyId, CancellationToken ct)
    {
        var model = new UnitFormViewModel { PropertyId = propertyId };
        await PopulateUnitTypeOptionsAsync(model, currentUnitTypeId: null, ct);
        return PartialView("_UnitForm", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int propertyId, UnitFormViewModel model, CancellationToken ct)
    {
        model.PropertyId = propertyId;

        if (!ModelState.IsValid)
        {
            await PopulateUnitTypeOptionsAsync(model, currentUnitTypeId: null, ct);
            return PartialView("_UnitForm", model);
        }

        var result = await unitService.CreateAsync(ToInput(model), ct);
        if (!result.Succeeded)
        {
            ModelState.AddErrors(result.Errors);
            await PopulateUnitTypeOptionsAsync(model, currentUnitTypeId: null, ct);
            return PartialView("_UnitForm", model);
        }

        return Json(new { success = true });
    }

    [HttpGet("{unitId:int}/Edit")]
    public async Task<IActionResult> EditForm(int propertyId, int unitId, CancellationToken ct)
    {
        var unit = await unitService.GetActiveByIdAsync(unitId, ct);
        if (unit is null || unit.PropertyId != propertyId)
        {
            return NotFound();
        }

        var model = new UnitFormViewModel
        {
            Id = unit.Id,
            PropertyId = propertyId,
            UnitNumber = unit.UnitNumber,
            Bedrooms = unit.Bedrooms,
            Bathrooms = unit.Bathrooms,
            MonthlyRent = unit.MonthlyRent,
            UnitTypeId = unit.UnitTypeId,
        };
        await PopulateUnitTypeOptionsAsync(model, currentUnitTypeId: unit.UnitTypeId, ct);
        return PartialView("_UnitForm", model);
    }

    [HttpPost("{unitId:int}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int propertyId, int unitId, UnitFormViewModel model, CancellationToken ct)
    {
        model.PropertyId = propertyId;
        model.Id = unitId;

        if (!ModelState.IsValid)
        {
            await PopulateUnitTypeOptionsAsync(model, currentUnitTypeId: model.UnitTypeId, ct);
            return PartialView("_UnitForm", model);
        }

        var result = await unitService.UpdateAsync(unitId, ToInput(model), ct);
        if (!result.Succeeded)
        {
            ModelState.AddErrors(result.Errors);
            await PopulateUnitTypeOptionsAsync(model, currentUnitTypeId: model.UnitTypeId, ct);
            return PartialView("_UnitForm", model);
        }

        return Json(new { success = true });
    }

    [HttpPost("{unitId:int}/Remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int propertyId, int unitId, CancellationToken ct)
    {
        var result = await unitService.RemoveAsync(unitId, ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Errors.Values.SelectMany(e => e).FirstOrDefault() });
        }

        return Json(new { success = true });
    }

    [HttpGet("~/Properties/{propertyId:int}/UnitsPartial")]
    public IActionResult UnitsPartial(int propertyId)
    {
        return ViewComponent("UnitList", new { propertyId });
    }

    private async Task PopulateUnitTypeOptionsAsync(UnitFormViewModel model, int? currentUnitTypeId, CancellationToken ct)
    {
        var types = await unitService.GetSelectableUnitTypesAsync(currentUnitTypeId, ct);
        model.UnitTypeOptions = types
            .Select(t => new SelectListItem(t.Name, t.Id.ToString()) { Selected = t.Id == model.UnitTypeId })
            .ToList();
    }

    private static UnitInput ToInput(UnitFormViewModel model) =>
        new(model.PropertyId, model.UnitNumber, model.Bedrooms, model.Bathrooms, model.MonthlyRent, model.UnitTypeId!.Value);
}
