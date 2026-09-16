namespace PropertyManagement.Domain.Validation;

/// <summary>One outstanding validation issue, tied to the specific field (and, for
/// repeating data, the specific item) it belongs to (Features/13, VALID-2).</summary>
public record FieldError(string Field, string Message);
