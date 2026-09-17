namespace PropertyManagement.Web.Models;

/// <summary>One column of the reusable Bonus 1 grid (Features/10, GRID-3).</summary>
/// <param name="Width">Optional CSS width for <c>table-fixed</c> colgroup (e.g. "42%").</param>
public record GridColumn(
    string Key,
    string Header,
    bool Sortable = true,
    string Type = "text",
    string? Width = null);
