using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PropertyManagement.Web.Extensions;

public static class ModelStateExtensions
{
    /// <summary>Maps a <see cref="Domain.Common.ServiceResult{T}"/>'s field-keyed errors into
    /// ModelState, so a service's validation failures re-render the same partial with the
    /// error attached to the field it belongs to (empty key = form-level).</summary>
    public static void AddErrors(this ModelStateDictionary modelState, IReadOnlyDictionary<string, string[]> errors)
    {
        foreach (var (field, messages) in errors)
        {
            foreach (var message in messages)
            {
                modelState.AddModelError(field, message);
            }
        }
    }
}
