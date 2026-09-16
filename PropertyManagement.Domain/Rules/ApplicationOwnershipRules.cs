using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Domain.Rules;

/// <summary>
/// "Is the current user one of this application's applicants" — the ownership check
/// every applicant-facing mutation and the list scope must use (Features/14, MULTI-1:
/// a set, not a single owner field). Defined once so the list query, the wizard's
/// resource lookup, and the browse-apply resume check can't drift apart.
/// </summary>
public static class ApplicationOwnershipRules
{
    public static bool IsOwnedBy(this Application application, string userId) =>
        application.Applicants.Any(a => a.UserId == userId);

    public static IQueryable<Application> OwnedBy(this IQueryable<Application> applications, string userId) =>
        applications.Where(a => a.Applicants.Any(x => x.UserId == userId));
}
