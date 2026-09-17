namespace PropertyManagement.Domain.Entities;

public class Unit
{
    public int Id { get; set; }

    public int PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public string UnitNumber { get; set; } = string.Empty;
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public decimal MonthlyRent { get; set; }

    public int UnitTypeId { get; set; }
    public UnitType UnitType { get; set; } = null!;

    /// <summary>Soft-delete flag; removal never hard-deletes (Features/02, PROP-4).</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<Lease> Leases { get; set; } = new List<Lease>();
    public ICollection<Application> Applications { get; set; } = new List<Application>();
}
