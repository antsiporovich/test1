namespace PropertyManagement.Web.Models;

public class StatusHistoryRowViewModel
{
    public string ResultingStatus { get; set; } = "";
    public string ActorName { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
    public string? Comment { get; set; }
}
