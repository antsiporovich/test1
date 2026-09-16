namespace PropertyManagement.Web.Models;

/// <summary>One column of a <see cref="GridViewModel"/> — deliberately generic, no
/// Applications-specific concepts, so the grid component can be reused for any list.
/// <paramref name="Type"/> is a display hint for the client (e.g. "date" formats the
/// cell via the browser's locale instead of showing the raw ISO string); anything
/// else renders as plain text.</summary>
public record GridColumn(string Key, string Header, bool Sortable = true, string Type = "text");
