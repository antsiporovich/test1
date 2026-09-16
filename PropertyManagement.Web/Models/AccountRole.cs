namespace PropertyManagement.Web.Models;

/// <summary>
/// Registration-time role choice. Values match the seeded ASP.NET Identity role
/// names exactly (<see cref="Enum.ToString()"/> is used directly as the role name),
/// so there is exactly one place that maps "what the user picked" to "what role they
/// get" — the enum itself.
/// </summary>
public enum AccountRole
{
    Applicant,
    PropertyManager
}
