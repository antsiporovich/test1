namespace PropertyManagement.Domain.Entities;

/// <summary>Lookup table. Inactive stays valid on units already using it but cannot be
/// selected for a new or changed unit (server-enforced elsewhere, not here).</summary>
public class UnitType
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Unit> Units { get; set; } = new List<Unit>();
}
