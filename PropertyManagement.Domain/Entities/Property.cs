namespace PropertyManagement.Domain.Entities;

public class Property
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    /// <summary>Soft-delete flag; removal never hard-deletes (Features/02, PROP-4).</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<Unit> Units { get; set; } = new List<Unit>();
}
