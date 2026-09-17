using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Web.Models;

namespace PropertyManagement.Web.ViewComponents;

/// <summary>
/// Self-contained widget: given an application id, loads and renders its residence
/// rows — independent of whatever the host page already has in hand. Reused both for
/// the wizard's Residence History step and as the AJAX-refresh target after any
/// residence add/edit/remove (Features/05).
/// </summary>
public class ResidenceHistorySectionViewComponent(IApplicationQueryService queryService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int applicationId, bool isEditable)
    {
        var residences = await queryService.GetResidencesForApplicationAsync(applicationId);

        var model = new ResidenceHistoryListViewModel
        {
            ApplicationId = applicationId,
            IsEditable = isEditable,
            Residences = residences.Select(r => new ResidenceRowViewModel
            {
                Id = r.Id,
                AddressLine1 = r.AddressLine1,
                AddressLine2 = r.AddressLine2,
                City = r.City,
                State = r.State,
                ZipCode = r.ZipCode,
                LandlordName = r.LandlordName,
                LandlordPhone = r.LandlordPhone,
                MoveInDate = r.MoveInDate,
                MoveOutDate = r.MoveOutDate,
            }).ToList(),
        };

        return View(model);
    }
}
