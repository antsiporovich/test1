using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Rules;

/// <summary>
/// Terminal-status classification, shared by every place that needs "is this
/// application still open" (unit removal guard here in Epic 2; withdraw
/// eligibility and review transitions in later epics).
/// </summary>
public static class ApplicationStatusRules
{
    /// <summary>
    /// Use this array directly inside an EF Core <c>Where</c> clause
    /// (<c>TerminalStatuses.Contains(a.Status)</c>) so it translates to a SQL
    /// <c>IN</c> clause. <see cref="IsTerminal"/> wraps a method call and does
    /// NOT translate — only use it against an already-materialized value.
    /// </summary>
    public static readonly ApplicationStatus[] TerminalStatuses =
    [
        ApplicationStatus.Approved,
        ApplicationStatus.Denied,
        ApplicationStatus.Withdrawn,
    ];

    public static bool IsTerminal(this ApplicationStatus status) => TerminalStatuses.Contains(status);
}
