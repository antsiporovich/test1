using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class ApplicationQueryService(AppDbContext db) : IApplicationQueryService
{
    public IQueryable<Application> BuildQuery(string userId, bool isApplicant, ApplicationStatus? status = null, int? propertyId = null)
    {
        var query = db.Applications.AsQueryable();

        // Ownership scope applied first so a filter can never widen past it (LIST-1/LIST-3).
        if (isApplicant)
        {
            query = query.Where(a => a.Applicants.Any(x => x.UserId == userId));
        }

        if (status is not null)
        {
            query = query.Where(a => a.Status == status);
        }

        if (propertyId is not null)
        {
            query = query.Where(a => a.Unit.PropertyId == propertyId);
        }

        return query;
    }
}
