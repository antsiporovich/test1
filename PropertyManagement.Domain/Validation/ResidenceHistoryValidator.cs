using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Domain.Validation;

/// <summary>
/// The single definition of "is Residence History complete" (Features/13,
/// VALID-2). Zero residences is valid (Features/04, WIZ-7) — this only flags
/// individual residences with bad data, never an empty list.
/// </summary>
public static class ResidenceHistoryValidator
{
    public static IEnumerable<FieldError> Validate(IEnumerable<Residence> residences)
    {
        var index = 0;
        foreach (var residence in residences)
        {
            index++;
            if (residence.MoveOutDate is { } moveOut && moveOut < residence.MoveInDate)
            {
                yield return new FieldError($"Residence {index}", "Move-out date cannot be before move-in date.");
            }
        }
    }
}
