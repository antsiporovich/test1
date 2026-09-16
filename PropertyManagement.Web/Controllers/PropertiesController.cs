using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Web.Extensions;
using PropertyManagement.Web.Models;

namespace PropertyManagement.Web.Controllers;

[Authorize(Roles = "PropertyManager")]
[Route("Properties")]
public class PropertiesController(IPropertyService propertyService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        return View(await BuildRowsAsync(ct));
    }

    [HttpGet("ListPartial")]
    public async Task<IActionResult> ListPartial(CancellationToken ct)
    {
        return PartialView("_PropertyList", await BuildRowsAsync(ct));
    }

    [HttpGet("Create")]
    public IActionResult CreateForm()
    {
        return PartialView("_PropertyForm", new PropertyFormViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PropertyFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_PropertyForm", model);
        }

        var result = await propertyService.CreateAsync(ToInput(model), ct);
        if (!result.Succeeded)
        {
            ModelState.AddErrors(result.Errors);
            return PartialView("_PropertyForm", model);
        }

        return Json(new { success = true });
    }

    [HttpGet("{id:int}/Edit")]
    public async Task<IActionResult> EditForm(int id, CancellationToken ct)
    {
        var property = await propertyService.GetActiveByIdAsync(id, ct);
        if (property is null)
        {
            return NotFound();
        }

        var model = new PropertyFormViewModel
        {
            Id = property.Id,
            Name = property.Name,
            AddressLine1 = property.AddressLine1,
            AddressLine2 = property.AddressLine2,
            City = property.City,
            State = property.State,
            ZipCode = property.ZipCode,
        };
        return PartialView("_PropertyForm", model);
    }

    [HttpPost("{id:int}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PropertyFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_PropertyForm", model);
        }

        var result = await propertyService.UpdateAsync(id, ToInput(model), ct);
        if (!result.Succeeded)
        {
            ModelState.AddErrors(result.Errors);
            return PartialView("_PropertyForm", model);
        }

        return Json(new { success = true });
    }

    [HttpPost("{id:int}/Remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id, CancellationToken ct)
    {
        var result = await propertyService.RemoveAsync(id, ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Errors.Values.SelectMany(e => e).FirstOrDefault() });
        }

        return Json(new { success = true });
    }

    private async Task<List<PropertyRowViewModel>> BuildRowsAsync(CancellationToken ct)
    {
        var properties = await propertyService.GetActivePropertiesAsync(ct);
        return properties.Select(p => new PropertyRowViewModel
        {
            Id = p.Id,
            Name = p.Name,
            Address = $"{p.AddressLine1}, {p.City}, {p.State} {p.ZipCode}",
            UnitCount = p.Units.Count,
        }).ToList();
    }

    private static PropertyInput ToInput(PropertyFormViewModel model) =>
        new(model.Name, model.AddressLine1, model.AddressLine2, model.City, model.State, model.ZipCode);
}
