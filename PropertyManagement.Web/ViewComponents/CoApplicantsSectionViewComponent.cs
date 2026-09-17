using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Web.Models;

namespace PropertyManagement.Web.ViewComponents;

/// <summary>
/// Self-contained widget: given an application id, loads and renders its co-applicants
/// (Features/14, MULTI-1/MULTI-2) — independent of whatever the host page already has in
/// hand. Reused both for the Summary step and as the AJAX-refresh target after adding one.
/// </summary>
public class CoApplicantsSectionViewComponent(IApplicationQueryService queryService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int applicationId, bool isEditable)
    {
        var applicants = (await queryService.GetCoApplicantsAsync(applicationId))
            .Select(a => new CoApplicantRowViewModel
            {
                DisplayName = a.DisplayName,
                Email = a.Email,
            })
            .ToList();

        return View(new CoApplicantsListViewModel { ApplicationId = applicationId, IsEditable = isEditable, Applicants = applicants });
    }
}
