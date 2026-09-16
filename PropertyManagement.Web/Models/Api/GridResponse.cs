namespace PropertyManagement.Web.Models.Api;

/// <summary>Shape returned by every Bonus 1 grid JSON endpoint: one page of rows plus the filtered (not global) total row count.</summary>
public class GridResponse<T>
{
    public List<T> Rows { get; set; } = [];
    public int TotalCount { get; set; }
}
