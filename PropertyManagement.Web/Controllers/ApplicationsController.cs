using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;
using PropertyManagement.Infrastructure.Data;
using PropertyManagement.Infrastructure.Identity;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Web.Models;

namespace PropertyManagement.Web.Controllers;

[Authorize]
[Route("Applications")]
public class ApplicationsController(
    IApplicationQueryService queryService,
    IApplicationService applicationService,
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

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Applicant")]
    public async Task<IActionResult> Wizard(int id, CancellationToken ct)
    {
        var application = await LoadOwnedApplicationAsync(id, ct);
        if (application is null)
        {
            return NotFound();
        }

        var vm = await BuildWizardViewModelAsync(application, WizardStepRules.InitialStep(application), ct);
        return View("Wizard", vm);
    }

    [HttpPost("{id:int}")]
    [Authorize(Roles = "Applicant")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Wizard(int id, ApplicationWizardViewModel model, string command, CancellationToken ct)
    {
        var application = await LoadOwnedApplicationAsync(id, ct);
        if (application is null)
        {
            return NotFound();
        }

        // Independent re-check before mutating anything — never trust the posted step/IsEditable (WIZ-5).
        if (!application.Status.IsEditable())
        {
            return Forbid();
        }

        switch (command)
        {
            case "Back":
                var previousStep = model.CurrentStep == WizardStep.Summary
                    ? WizardStep.ResidenceHistory
                    : WizardStep.ApplicantInformation;
                // Fresh state being shown, not a failed resubmit — clear ModelState so
                // asp-for renders the new model values, not the just-posted raw ones
                // (tag helpers prefer ModelState's cached value over the model whenever
                // a key exists there at all, valid or not).
                ModelState.Clear();
                return View("Wizard", await BuildWizardViewModelAsync(application, previousStep, ct));

            case "Continue" when model.CurrentStep == WizardStep.ApplicantInformation:
                if (!TryValidateModel(model.ApplicantInformation, nameof(model.ApplicantInformation)))
                {
                    return View("Wizard", await BuildWizardViewModelAsync(application, WizardStep.ApplicantInformation, ct, model.ApplicantInformation));
                }

                await applicationService.SaveApplicantInfoAsync(application, ToInput(model.ApplicantInformation), ct);
                ModelState.Clear();
                return View("Wizard", await BuildWizardViewModelAsync(application, WizardStep.ResidenceHistory, ct));

            case "Continue" when model.CurrentStep == WizardStep.ResidenceHistory:
                await applicationService.ConfirmResidenceHistoryAsync(application, ct);
                ModelState.Clear();
                return View("Wizard", await BuildWizardViewModelAsync(application, WizardStep.Summary, ct));

            case "Submit" when model.CurrentStep == WizardStep.Summary:
                var result = await applicationService.SubmitAsync(application, userManager.GetUserId(User)!, ct);
                if (!result.Succeeded)
                {
                    ModelState.Clear();
                    var errorVm = await BuildWizardViewModelAsync(application, WizardStep.Summary, ct);
                    errorVm.SubmitError = result.Errors.Values.SelectMany(e => e).FirstOrDefault();
                    return View("Wizard", errorVm);
                }

                return RedirectToAction(nameof(Index));

            default:
                return BadRequest();
        }
    }

    [HttpPost("{id:int}/Withdraw")]
    [Authorize(Roles = "Applicant")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int id, CancellationToken ct)
    {
        var application = await LoadOwnedApplicationAsync(id, ct);
        if (application is null)
        {
            return NotFound();
        }

        var result = await applicationService.WithdrawAsync(application, userManager.GetUserId(User)!, ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Errors.Values.SelectMany(e => e).FirstOrDefault() });
        }

        return RedirectToAction(nameof(Index));
    }

    // Route/parameter uses "applicationId", not "id" — ASP.NET's ModelState keys are
    // case-insensitive, so an "id" action parameter here would silently collide with
    // ResidenceFormViewModel.Id (a *different* entity's id) and asp-for="Id" would
    // render the wrong value from ModelState instead of the model (confirmed live).
    [HttpGet("{applicationId:int}/Residences/Create")]
    [Authorize(Roles = "Applicant")]
    public async Task<IActionResult> CreateResidenceForm(int applicationId, CancellationToken ct)
    {
        var application = await LoadOwnedApplicationAsync(applicationId, ct);
        if (application is null)
        {
            return NotFound();
        }

        if (!application.Status.IsEditable())
        {
            return Forbid();
        }

        return PartialView("_ResidenceForm", new ResidenceFormViewModel { ApplicationId = applicationId });
    }

    [HttpPost("{applicationId:int}/Residences/Create")]
    [Authorize(Roles = "Applicant")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateResidence(int applicationId, ResidenceFormViewModel model, CancellationToken ct)
    {
        model.ApplicationId = applicationId;

        var application = await LoadOwnedApplicationAsync(applicationId, ct);
        if (application is null)
        {
            return NotFound();
        }

        if (!application.Status.IsEditable())
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return PartialView("_ResidenceForm", model);
        }

        await applicationService.AddResidenceAsync(application, ToInput(model), ct);
        return Json(new { success = true });
    }

    [HttpGet("{applicationId:int}/Residences/{residenceId:int}/Edit")]
    [Authorize(Roles = "Applicant")]
    public async Task<IActionResult> EditResidenceForm(int applicationId, int residenceId, CancellationToken ct)
    {
        var application = await LoadOwnedApplicationAsync(applicationId, ct);
        if (application is null)
        {
            return NotFound();
        }

        if (!application.Status.IsEditable())
        {
            return Forbid();
        }

        var residence = application.Residences.FirstOrDefault(r => r.Id == residenceId);
        if (residence is null)
        {
            return NotFound();
        }

        var model = new ResidenceFormViewModel
        {
            Id = residence.Id,
            ApplicationId = applicationId,
            AddressLine1 = residence.AddressLine1,
            AddressLine2 = residence.AddressLine2,
            City = residence.City,
            State = residence.State,
            ZipCode = residence.ZipCode,
            LandlordName = residence.LandlordName,
            LandlordPhone = residence.LandlordPhone,
            MoveInDate = residence.MoveInDate,
            MoveOutDate = residence.MoveOutDate,
        };
        return PartialView("_ResidenceForm", model);
    }

    [HttpPost("{applicationId:int}/Residences/{residenceId:int}/Edit")]
    [Authorize(Roles = "Applicant")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditResidence(int applicationId, int residenceId, ResidenceFormViewModel model, CancellationToken ct)
    {
        model.Id = residenceId;
        model.ApplicationId = applicationId;

        var application = await LoadOwnedApplicationAsync(applicationId, ct);
        if (application is null)
        {
            return NotFound();
        }

        if (!application.Status.IsEditable())
        {
            return Forbid();
        }

        var residence = application.Residences.FirstOrDefault(r => r.Id == residenceId);
        if (residence is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return PartialView("_ResidenceForm", model);
        }

        await applicationService.UpdateResidenceAsync(residence, ToInput(model), ct);
        return Json(new { success = true });
    }

    [HttpPost("{applicationId:int}/Residences/{residenceId:int}/Remove")]
    [Authorize(Roles = "Applicant")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveResidence(int applicationId, int residenceId, CancellationToken ct)
    {
        var application = await LoadOwnedApplicationAsync(applicationId, ct);
        if (application is null)
        {
            return NotFound();
        }

        if (!application.Status.IsEditable())
        {
            return Forbid();
        }

        var residence = application.Residences.FirstOrDefault(r => r.Id == residenceId);
        if (residence is null)
        {
            return NotFound();
        }

        await applicationService.RemoveResidenceAsync(residence, ct);
        return Json(new { success = true });
    }

    [HttpGet("{applicationId:int}/ResidenceHistoryPartial")]
    [Authorize(Roles = "Applicant")]
    public async Task<IActionResult> ResidenceHistoryPartial(int applicationId, CancellationToken ct)
    {
        var application = await LoadOwnedApplicationAsync(applicationId, ct);
        if (application is null)
        {
            return NotFound();
        }

        return ViewComponent("ResidenceHistorySection", new { applicationId, isEditable = application.Status.IsEditable() });
    }

    private async Task<Application?> LoadOwnedApplicationAsync(int id, CancellationToken ct)
    {
        var userId = userManager.GetUserId(User)!;
        return await db.Applications
            .Include(a => a.Unit)
            .Include(a => a.ApplicantInfo)
            .Include(a => a.Residences)
            .Include(a => a.Applicants)
            .Include(a => a.StatusHistory)
            .OwnedBy(userId)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    private async Task<ApplicationWizardViewModel> BuildWizardViewModelAsync(
        Application application, WizardStep step, CancellationToken ct, ApplicantInfoSectionViewModel? applicantInfoOverride = null)
    {
        var vm = new ApplicationWizardViewModel
        {
            ApplicationId = application.Id,
            CurrentStep = step,
            IsEditable = application.Status.IsEditable(),
            Status = application.Status.ToString(),
            ApplicantInformation = applicantInfoOverride ?? MapApplicantInfo(application.ApplicantInfo),
        };

        if (application.Status == ApplicationStatus.Returned)
        {
            vm.ReturnComment = await db.ApplicationStatusHistories
                .Where(h => h.ApplicationId == application.Id && h.ToStatus == ApplicationStatus.Returned)
                .OrderByDescending(h => h.Timestamp)
                .Select(h => h.Comment)
                .FirstOrDefaultAsync(ct);
        }

        if (step == WizardStep.Summary)
        {
            vm.Residences = application.Residences.Select(r => new ResidenceRowViewModel
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
            }).ToList();
        }

        return vm;
    }

    private static ApplicantInfoSectionViewModel MapApplicantInfo(ApplicantInfo? info) => info is null
        ? new ApplicantInfoSectionViewModel()
        : new ApplicantInfoSectionViewModel
        {
            FullName = info.FullName,
            Phone = info.Phone,
            Email = info.Email,
            AddressLine1 = info.AddressLine1,
            AddressLine2 = info.AddressLine2,
            City = info.City,
            State = info.State,
            ZipCode = info.ZipCode,
        };

    private static ApplicantInfoInput ToInput(ApplicantInfoSectionViewModel model) =>
        new(model.FullName, model.Phone, model.Email, model.AddressLine1, model.AddressLine2, model.City, model.State, model.ZipCode);

    private static ResidenceInput ToInput(ResidenceFormViewModel model) =>
        new(model.AddressLine1, model.AddressLine2, model.City, model.State, model.ZipCode, model.LandlordName, model.LandlordPhone, model.MoveInDate, model.MoveOutDate);

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
                IsWithdrawable = !ApplicationStatusRules.TerminalStatuses.Contains(a.Status),
            })
            .AsNoTracking()
            .ToListAsync(ct);
}
