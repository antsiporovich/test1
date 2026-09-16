using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class PropertyService(AppDbContext db) : IPropertyService
{
    public async Task<List<Property>> GetActivePropertiesAsync(CancellationToken ct = default) =>
        await db.Properties
            .Where(p => p.IsActive)
            .Include(p => p.Units.Where(u => u.IsActive))
            .OrderBy(p => p.Name)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<Property?> GetActiveByIdAsync(int id, CancellationToken ct = default) =>
        await db.Properties
            .Where(p => p.IsActive && p.Id == id)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

    public async Task<ServiceResult<Property>> CreateAsync(PropertyInput input, CancellationToken ct = default)
    {
        var property = new Property
        {
            Name = input.Name,
            AddressLine1 = input.AddressLine1,
            AddressLine2 = input.AddressLine2,
            City = input.City,
            State = input.State,
            ZipCode = input.ZipCode,
            IsActive = true,
        };

        db.Properties.Add(property);
        await db.SaveChangesAsync(ct);
        return ServiceResult<Property>.Success(property);
    }

    public async Task<ServiceResult<Property>> UpdateAsync(int id, PropertyInput input, CancellationToken ct = default)
    {
        var property = await db.Properties.FirstOrDefaultAsync(p => p.IsActive && p.Id == id, ct);
        if (property is null)
        {
            return ServiceResult<Property>.Fail(string.Empty, "Property not found.");
        }

        property.Name = input.Name;
        property.AddressLine1 = input.AddressLine1;
        property.AddressLine2 = input.AddressLine2;
        property.City = input.City;
        property.State = input.State;
        property.ZipCode = input.ZipCode;

        await db.SaveChangesAsync(ct);
        return ServiceResult<Property>.Success(property);
    }

    public async Task<ServiceResult<bool>> RemoveAsync(int id, CancellationToken ct = default)
    {
        var property = await db.Properties
            .Include(p => p.Units)
            .FirstOrDefaultAsync(p => p.IsActive && p.Id == id, ct);

        if (property is null)
        {
            return ServiceResult<bool>.Fail(string.Empty, "Property not found.");
        }

        property.IsActive = false;
        foreach (var unit in property.Units)
        {
            unit.IsActive = false;
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }
}
