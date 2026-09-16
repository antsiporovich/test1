using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Data;
using PropertyManagement.Infrastructure.Identity;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Web.Models;

namespace PropertyManagement.Web.Controllers;

[Authorize]
[Route("Applications")]
public class ApplicationsController(
    IApplicationQueryService queryService,
    UserManager<ApplicationUser> userManager,
    AppDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(ApplicationStatus? status, int? propertyId, CancellationToken ct)
    {
        var userId = userManager.GetUserId(User)!;
        var isApplicant = User.IsInRole("Applicant");

        var vm = new ApplicationListFilterViewModel
        {
            Status = status,
            PropertyId = propertyId,
            StatusOptions = Enum.GetValues<ApplicationStatus>()
                .Select(s => new SelectListItem(s.ToString(), s.ToString()))
                .ToList(),
            PropertyOptions = await db.Properties
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .Select(p => new SelectListItem(p.Name, p.Id.ToString()))
                .ToListAsync(ct),
            Rows = await BuildRowsAsync(userId, isApplicant, status, propertyId, ct),
        };

        return View(vm);
    }

    private async Task<List<ApplicationListRowViewModel>> BuildRowsAsync(
        string userId, bool isApplicant, ApplicationStatus? status, int? propertyId, CancellationToken ct) =>
        await queryService.BuildQuery(userId, isApplicant, status, propertyId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new ApplicationListRowViewModel
            {
                Id = a.Id,
                PropertyName = a.Unit.Property.Name,
                UnitNumber = a.Unit.UnitNumber,
                Status = a.Status.ToString(),
                ApplicantName = a.ApplicantInfo != null ? a.ApplicantInfo.FullName : null,
                LastUpdatedAtUtc = a.StatusHistory.Any() ? a.StatusHistory.Max(h => h.Timestamp) : a.CreatedAtUtc,
            })
            .AsNoTracking()
            .ToListAsync(ct);
}
