namespace PropertyManagement.Domain.Common;

/// <summary>
/// Outcome of a service-layer operation, with errors keyed by the field they
/// belong to (empty string for form-level errors) — the shape Bonus 4's
/// "errors returned to the field they belong to" needs, so the same result type
/// carries every service's validation failures back to a controller's
/// <c>ModelState</c> without a bespoke exception type per rule.
/// </summary>
public class ServiceResult<T>
{
    private ServiceResult(bool succeeded, T? value, IReadOnlyDictionary<string, string[]> errors)
    {
        Succeeded = succeeded;
        Value = value;
        Errors = errors;
    }

    public bool Succeeded { get; }
    public T? Value { get; }
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public static ServiceResult<T> Success(T value) =>
        new(true, value, new Dictionary<string, string[]>());

    public static ServiceResult<T> Fail(string field, string message) =>
        new(false, default, new Dictionary<string, string[]> { [field] = [message] });
}
