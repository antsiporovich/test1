using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Infrastructure.Data;
using PropertyManagement.Web.Models;

namespace PropertyManagement.Web.ViewComponents;

/// <summary>
/// Self-contained widget: given an application id, loads and renders its co-applicants
/// (Features/14, MULTI-1/MULTI-2) — independent of whatever the host page already has in
/// hand. Reused both for the Summary step and as the AJAX-refresh target after adding one.
/// </summary>
public class CoApplicantsSectionViewComponent(AppDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int applicationId, bool isEditable)
    {
        var applicants = await db.ApplicationApplicants
            .Where(a => a.ApplicationId == applicationId)
            .Join(db.Users, a => a.UserId, u => u.Id, (a, u) => new CoApplicantRowViewModel
            {
                DisplayName = u.DisplayName,
                Email = u.Email!,
            })
            .OrderBy(a => a.DisplayName)
            .AsNoTracking()
            .ToListAsync();

        return View(new CoApplicantsListViewModel { ApplicationId = applicationId, IsEditable = isEditable, Applicants = applicants });
    }
}
