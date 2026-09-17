using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Identity;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Web.Models.Api;

namespace PropertyManagement.Web.Controllers.Api;

/// <summary>Bonus 1 JSON endpoint backing the Applications grid (Features/10, GRID-4).</summary>
[Authorize]
[ApiController]
[Route("api/applications")]
public class ApplicationsApiController(IApplicationQueryService queryService, UserManager<ApplicationUser> userManager) : ControllerBase
{
    private static readonly HashSet<string> SortableKeys = ["property", "status", "updated"];
    private const int MaxPageSize = 100;

    /// <summary>
    /// Returns one page of the caller's scoped, filtered Applications, plus the
    /// filtered total row count — Applicants see only their own applications, Property
    /// Managers see all (same ownership scope as the server-rendered list, LIST-1/LIST-2).
    /// </summary>
    /// <param name="status">Optional status filter.</param>
    /// <param name="propertyId">Optional property filter.</param>
    /// <param name="sort">Sort column: <c>property</c>, <c>status</c>, or <c>updated</c>. Unrecognized values fall back to the default sort.</param>
    /// <param name="desc">Sort descending instead of ascending.</param>
    /// <param name="page">1-based page number; clamped to at least 1.</param>
    /// <param name="pageSize">Rows per page; clamped to 1–100.</param>
    /// <param name="search">Optional applicant-name search (PM view only — has no effect narrowing an Applicant's own scope beyond their own rows).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType<GridResponse<ApplicationRowDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GridResponse<ApplicationRowDto>>> Get(
        ApplicationStatus? status,
        int? propertyId,
        string? sort,
        bool desc = false,
        int page = 1,
        int pageSize = 10,
        string? search = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var sortKey = sort is not null && SortableKeys.Contains(sort) ? sort : null;

        var userId = userManager.GetUserId(User)!;
        var isApplicant = User.IsInRole("Applicant");

        var query = queryService.BuildQuery(userId, isApplicant, status, propertyId, sortKey, desc, search);

        var totalCount = await query.CountAsync(ct);
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ApplicationRowDto
            {
                Id = a.Id,
                Applicant = a.ApplicantInfo != null && a.ApplicantInfo.FullName != ""
                    ? a.ApplicantInfo.FullName + " — " + (
                        userManager.Users
                            .Where(u => u.Id == a.Applicants.OrderBy(x => x.AddedAtUtc).Select(x => x.UserId).FirstOrDefault())
                            .Select(u => u.Email)
                            .FirstOrDefault()
                        ?? a.ApplicantInfo.Email)
                    : userManager.Users
                        .Where(u => u.Id == a.Applicants.OrderBy(x => x.AddedAtUtc).Select(x => x.UserId).FirstOrDefault())
                        .Select(u => (u.DisplayName ?? u.Email ?? "Applicant") + " — " + u.Email)
                        .FirstOrDefault()
                      ?? "Applicant",
                Property = a.Unit.Property.Name + " — " + a.Unit.Property.AddressLine1 + ", " + a.Unit.Property.City + " · Unit " + a.Unit.UnitNumber,
                PropertyName = a.Unit.Property.Name,
                Unit = a.Unit.UnitNumber,
                PropertyAddress = a.Unit.Property.AddressLine1 + ", " + a.Unit.Property.City,
                Bedrooms = a.Unit.Bedrooms,
                Bathrooms = a.Unit.Bathrooms,
                Status = a.Status == ApplicationStatus.UnderReview
                    ? "Under Review (" + userManager.Users.Where(u => u.Id == a.ClaimedByUserId).Select(u => u.DisplayName).FirstOrDefault() + ")"
                    : a.Status.ToString(),
                Updated = a.StatusHistory.Any() ? a.StatusHistory.Max(h => h.Timestamp) : a.CreatedAtUtc,
            })
            .ToListAsync(ct);

        return Ok(new GridResponse<ApplicationRowDto> { Rows = rows, TotalCount = totalCount });
    }
}
