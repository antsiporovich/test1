using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Services;

public record PropertyInput(string Name, string AddressLine1, string? AddressLine2, string City, string State, string ZipCode);

public interface IPropertyService
{
    /// <summary>Active properties only, with their units eager-loaded for the unit-count display.</summary>
    Task<List<Property>> GetActivePropertiesAsync(CancellationToken ct = default);

    Task<Property?> GetActiveByIdAsync(int id, CancellationToken ct = default);

    Task<ServiceResult<Property>> CreateAsync(PropertyInput input, CancellationToken ct = default);

    Task<ServiceResult<Property>> UpdateAsync(int id, PropertyInput input, CancellationToken ct = default);

    /// <summary>Soft delete: marks the property and its units inactive. Never a physical delete.</summary>
    Task<ServiceResult<bool>> RemoveAsync(int id, CancellationToken ct = default);
}
