using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Web.Models;

public class ReviewQueueRowViewModel
{
    public int Id { get; set; }
    public string PropertyUnit { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
    public string? ClaimedByName { get; set; }
    public bool IsClaimedByMe { get; set; }
}
