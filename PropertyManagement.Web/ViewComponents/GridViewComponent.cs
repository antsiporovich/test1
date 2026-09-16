using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Web.Models;

namespace PropertyManagement.Web.ViewComponents;

/// <summary>
/// Generic reusable grid (Features/10, GRID-3): renders a table shell driven entirely
/// by <see cref="GridViewModel"/> and lets <c>grid.js</c> fetch/page/sort client-side —
/// no Application-specific code here, so it's plausible to reuse for a future list.
/// Synchronous: unlike the other view components in this app, this one does no I/O of
/// its own (the JSON endpoint does), so there's nothing to await.
/// </summary>
public class GridViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(GridViewModel model) => View(model);
}
