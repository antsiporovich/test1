namespace PropertyManagement.Web.Models;

/// <summary>Parameters for the reusable Bonus 1 grid view component (Features/10,
/// GRID-3). The component itself never mentions Applications — the caller supplies
/// the JSON endpoint, columns, page size, and any fixed query-string params (e.g. the
/// page's current filters) to send with every request.</summary>
public class GridViewModel
{
    public string ApiEndpoint { get; set; } = string.Empty;
    public List<GridColumn> Columns { get; set; } = [];
    public int PageSize { get; set; } = 10;
    public Dictionary<string, string> FixedParams { get; set; } = [];

    /// <summary>Optional URL template (e.g. "/Applications/{id}") substituting a row's
    /// "id" field; when set, rows are clickable. Null/empty disables row navigation.</summary>
    public string? RowUrlTemplate { get; set; }
}
