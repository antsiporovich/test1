using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;
using PropertyManagement.Domain.Validation;
using PropertyManagement.Infrastructure.Data;
using PropertyManagement.Infrastructure.Identity;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Domain.Common;
using PropertyManagement.Web.Models;

namespace PropertyManagement.Web.Controllers;

[Authorize]
[Route("Applications")]
public class ApplicationsController(
    IApplicationService applicationService,
    IApplicationQueryService queryService,
    UserManager<ApplicationUser> userManager,
    AppDbContext db) : Controller
{
    // Row data for this list now comes from the Bonus 1 grid (Features/10) fetching
    // /api/applications client-side — this action only builds the filter dropdowns
    // (and, for PMs, the separate Review Queue rows below the grid).
    [HttpGet("")]
    public async Task<IActionResult> Index(ApplicationStatus? status, int? propertyId, CancellationToken ct)
    {
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
        };

        if (!isApplicant)
        {
            var userId = userManager.GetUserId(User)!;
            vm.QueueRows = await queryService
                .BuildQuery(userId, isApplicant: false)
                .Where(a => a.Status == ApplicationStatus.Submitted || a.Status == ApplicationStatus.UnderReview)
                .OrderBy(a => a.Status).ThenBy(a => a.CreatedAtUtc)
                .Select(a => new ReviewQueueRowViewModel
                {
                    Id = a.Id,
                    PropertyUnit = a.Unit.Property.Name + " — Unit " + a.Unit.UnitNumber,
                    Status = a.Status,
                    ClaimedByName = a.ClaimedByUserId == null ? null : userManager.Users.Where(u => u.Id == a.ClaimedByUserId).Select(u => u.DisplayName).FirstOrDefault(),
                    IsClaimedByMe = a.ClaimedByUserId == userId,
                })
                .ToListAsync(ct);
        }

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
                // VALID-1: persists as-is and always advances, even with outstanding
                // validation issues — Summary lists them, Submit blocks on them.
                var saveResult = await applicationService.SaveApplicantInfoAsync(application, ToInput(model.ApplicantInformation), ct);
                ModelState.Clear();
                if (!saveResult.Succeeded)
                {
                    // MULTI-4: a co-applicant saved this section first. The failed
                    // SaveChangesAsync left ApplicantInfo's in-memory properties as our
                    // rejected edit — EF's identity map means even a fresh re-query would
                    // just hand back this same tracked (stale) instance, so reload it from
                    // the store explicitly to show their latest saved data, never ours.
                    await db.Entry(application.ApplicantInfo!).ReloadAsync(ct);
                    var conflictVm = await BuildWizardViewModelAsync(application, WizardStep.ApplicantInformation, ct);
                    conflictVm.ConcurrencyError = saveResult.Errors.Values.SelectMany(e => e).FirstOrDefault();
                    return View("Wizard", conflictVm);
                }

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

    [HttpGet("{applicationId:int}/Applicants/Add")]
    [Authorize(Roles = "Applicant")]
    public async Task<IActionResult> AddApplicantForm(int applicationId, CancellationToken ct)
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

        return PartialView("_CoApplicantForm", new AddApplicantFormViewModel { ApplicationId = applicationId });
    }

    [HttpPost("{applicationId:int}/Applicants/Add")]
    [Authorize(Roles = "Applicant")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddApplicant(int applicationId, AddApplicantFormViewModel model, CancellationToken ct)
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
            return PartialView("_CoApplicantForm", model);
        }

        var result = await applicationService.AddApplicantAsync(application, model.Email, ct);
        if (!result.Succeeded)
        {
            foreach (var (field, messages) in result.Errors)
            {
                foreach (var message in messages)
                {
                    ModelState.AddModelError(field, message);
                }
            }

            return PartialView("_CoApplicantForm", model);
        }

        return Json(new { success = true });
    }

    [HttpGet("{applicationId:int}/Applicants/ListPartial")]
    [Authorize(Roles = "Applicant")]
    public async Task<IActionResult> CoApplicantsPartial(int applicationId, CancellationToken ct)
    {
        var application = await LoadOwnedApplicationAsync(applicationId, ct);
        if (application is null)
        {
            return NotFound();
        }

        return ViewComponent("CoApplicantsSection", new { applicationId, isEditable = application.Status.IsEditable() });
    }

    [HttpPost("{id:int}/Claim")]
    [Authorize(Roles = "PropertyManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(int id, CancellationToken ct)
    {
        var result = await applicationService.ClaimAsync(id, userManager.GetUserId(User)!, ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Errors.Values.SelectMany(e => e).FirstOrDefault();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/Release")]
    [Authorize(Roles = "PropertyManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Release(int id, CancellationToken ct)
    {
        var userId = userManager.GetUserId(User)!;

        // Primary claimant-only check (QUEUE-2): a clean 403 for a mismatch, checked
        // before calling the service so a wrong-PM crafted request is rejected the same
        // way role/ownership mismatches are elsewhere (aspnet-identity-authorization.md).
        // ReleaseAsync's own conditional update is the backstop against a same-id race.
        var claimedBy = await db.Applications.Where(a => a.Id == id).Select(a => (string?)a.ClaimedByUserId).FirstOrDefaultAsync(ct);
        if (claimedBy != userId)
        {
            return Forbid();
        }

        var result = await applicationService.ReleaseAsync(id, userId, ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Errors.Values.SelectMany(e => e).FirstOrDefault();
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

        return PartialView("_ResidenceForm", MapResidence(residence, applicationId));
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

        var updateResult = await applicationService.UpdateResidenceAsync(residence, ToInput(model), ct);
        if (!updateResult.Succeeded)
        {
            // MULTI-4: re-render the modal with the co-applicant's latest saved data
            // (not this user's rejected edit) plus the "reload" message.
            ModelState.Clear();
            var fresh = await db.Residences.AsNoTracking().FirstOrDefaultAsync(r => r.Id == residenceId, ct);
            ModelState.AddModelError(string.Empty, updateResult.Errors.Values.SelectMany(e => e).FirstOrDefault() ?? "Could not save.");
            return PartialView("_ResidenceForm", fresh is null ? model : MapResidence(fresh, applicationId));
        }

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

    // ── PM Detail & Review ────────────────────────────────────────────────────

    /// <summary>REVIEW-1/REVIEW-5: PM read-only detail of any application, with
    /// full status/review history and a Review modal trigger.</summary>
    [HttpGet("{id:int}/Detail")]
    [Authorize(Roles = "PropertyManager")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var application = await LoadApplicationForPmAsync(id, ct);
        if (application is null) return NotFound();

        var vm = await BuildDetailViewModelAsync(application, ct);
        return View(vm);
    }

    [HttpGet("{id:int}/Review")]
    [Authorize(Roles = "PropertyManager")]
    public async Task<IActionResult> ReviewForm(int id, CancellationToken ct)
    {
        var application = await LoadApplicationForPmAsync(id, ct);
        if (application is null) return NotFound();

        if (!CanCurrentPmReview(application)) return Forbid();

        return PartialView("_ReviewForm", new ReviewFormViewModel { ApplicationId = id });
    }

    [HttpPost("{id:int}/Review")]
    [Authorize(Roles = "PropertyManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, ReviewFormViewModel model, CancellationToken ct)
    {
        model.ApplicationId = id;

        var application = await LoadApplicationForPmAsync(id, ct);
        if (application is null) return NotFound();

        if (!CanCurrentPmReview(application)) return Forbid();

        if (!ModelState.IsValid)
            return PartialView("_ReviewForm", model);

        var actorId = userManager.GetUserId(User)!;
        ServiceResult<bool> result = model.Outcome switch
        {
            ReviewOutcome.Approve => await applicationService.ApproveAsync(application, actorId, model.Comment, ct),
            ReviewOutcome.Return  => await applicationService.ReturnToApplicantAsync(application, actorId, model.Comment!, ct),
            ReviewOutcome.Deny    => await applicationService.DenyAsync(application, actorId, model.Comment!, ct),
            _                     => ServiceResult<bool>.Fail(string.Empty, "Invalid outcome."),
        };

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Errors.Values.SelectMany(e => e).FirstOrDefault() ?? "Review could not be completed.");
            return PartialView("_ReviewForm", model);
        }

        // ajaxModal.js: on data.redirect present, closes the modal and navigates there
        // instead of calling refreshRegion — correct for a whole-page status change.
        return Json(new { success = true, redirect = Url.Action(nameof(Detail), new { id }) });
    }

    // ── Applicant-side loaders & helpers ─────────────────────────────────────

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

    /// <summary>PM-side load — no ownership filter; PMs may view any application.</summary>
    private async Task<Application?> LoadApplicationForPmAsync(int id, CancellationToken ct) =>
        await db.Applications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.ApplicantInfo)
            .Include(a => a.Residences)
            .Include(a => a.Applicants)
            .Include(a => a.StatusHistory)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <summary>REVIEW-1: only the submitting PM (Submitted) or the claiming PM
    /// (UnderReview + claimedBy == me) may complete a review.</summary>
    private bool CanCurrentPmReview(Application application)
    {
        var userId = userManager.GetUserId(User)!;
        return application.Status == ApplicationStatus.Submitted
            || (application.Status == ApplicationStatus.UnderReview && application.ClaimedByUserId == userId);
    }

    private async Task<ApplicationDetailViewModel> BuildDetailViewModelAsync(Application application, CancellationToken ct)
    {
        // Resolve actor display names for the history table in one DB round-trip.
        var actorIds = application.StatusHistory.Select(h => h.ActorUserId).Distinct().ToList();
        var actorNames = await db.Users
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? u.UserName ?? u.Id, ct);

        // Collect applicant display names for the header.
        var applicantUserIds = application.Applicants.Select(a => a.UserId).ToList();
        var applicantNames = await db.Users
            .Where(u => applicantUserIds.Contains(u.Id))
            .Select(u => u.DisplayName ?? u.Email ?? u.Id)
            .ToListAsync(ct);

        return new ApplicationDetailViewModel
        {
            Id = application.Id,
            PropertyUnit = $"{application.Unit.Property.Name} — Unit {application.Unit.UnitNumber}",
            ApplicantNames = applicantNames.Count > 0 ? string.Join(", ", applicantNames) : "—",
            Status = application.Status.ToString(),
            CanReview = CanCurrentPmReview(application),
            ApplicantInformation = MapApplicantInfo(application.ApplicantInfo),
            Residences = application.Residences
                .OrderBy(r => r.MoveInDate)
                .Select(r => new ResidenceRowViewModel
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
                })
                .ToList(),
            StatusHistory = application.StatusHistory
                .OrderBy(h => h.Timestamp)
                .Select(h => new StatusHistoryRowViewModel
                {
                    ResultingStatus = h.ToStatus.ToString(),
                    ActorName = actorNames.TryGetValue(h.ActorUserId, out var name) ? name : h.ActorUserId,
                    Timestamp = h.Timestamp,
                    Comment = h.Comment,
                })
                .ToList(),
        };
    }

    private async Task<ApplicationWizardViewModel> BuildWizardViewModelAsync(
        Application application, WizardStep step, CancellationToken ct)
    {
        var vm = new ApplicationWizardViewModel
        {
            ApplicationId = application.Id,
            CurrentStep = step,
            IsEditable = application.Status.IsEditable(),
            IsWithdrawable = !application.Status.IsTerminal(),
            Status = application.Status.ToString(),
            ApplicantInformation = MapApplicantInfo(application.ApplicantInfo),
        };

        if (application.Status == ApplicationStatus.Returned)
        {
            vm.ReturnComment = await db.ApplicationStatusHistories
                .Where(h => h.ApplicationId == application.Id && h.ToStatus == ApplicationStatus.Returned)
                .OrderByDescending(h => h.Timestamp)
                .Select(h => h.Comment)
                .FirstOrDefaultAsync(ct);
        }

        // VALID-2: the same validator that feeds the Summary's outstanding list also
        // drives these inline field errors — reachable via Back or a fresh GET landing
        // back on a section that was saved-with-errors (VALID-1).
        if (step == WizardStep.ApplicantInformation && application.ApplicantInfo is not null)
        {
            foreach (var error in ApplicantInformationValidator.Validate(application.ApplicantInfo))
            {
                ModelState.AddModelError($"ApplicantInformation.{error.Field}", error.Message);
            }
        }

        if (step == WizardStep.Summary)
        {
            vm.OutstandingErrors = ApplicationValidation.GetOutstandingErrors(application).ToList();
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
            RowVersion = info.RowVersion,
        };

    private static ResidenceFormViewModel MapResidence(Residence residence, int applicationId) => new()
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
        RowVersion = residence.RowVersion,
    };

    // An empty posted form field binds a `string` property to null, not "" (that's how
    // [Required] catches "posted but blank"). VALID-1 now persists this section even
    // when invalid, so a blank field must still land as "" in the NOT NULL columns
    // below, not a runtime null the compile-time non-nullable annotations don't catch.
    private static ApplicantInfoInput ToInput(ApplicantInfoSectionViewModel model) =>
        new(model.FullName ?? "", model.Phone ?? "", model.Email ?? "", model.AddressLine1 ?? "", model.AddressLine2, model.City ?? "", model.State ?? "", model.ZipCode ?? "", model.RowVersion);

    private static ResidenceInput ToInput(ResidenceFormViewModel model) =>
        new(model.AddressLine1, model.AddressLine2, model.City, model.State, model.ZipCode, model.LandlordName, model.LandlordPhone, model.MoveInDate, model.MoveOutDate, model.RowVersion);
}
