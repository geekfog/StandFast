using StandFast.Domain.Common;

namespace StandFast.Application.Reporting;

/// <summary>The reports the reports page can show. The value is carried in the query string, so the names are part of the app's URLs.</summary>
public enum ReportKind
{
    PresentingOrderTimeline,
    StandupActivity,
}

/// <summary>Names the available reports for the report picker, so a report's title is written once rather than at every place it is shown.</summary>
public static class ReportCatalog
{
    public static readonly IReadOnlyList<ReportKind> All = [.. Enum.GetValues<ReportKind>()];

    public static ReportKind Default => ReportKind.PresentingOrderTimeline;

    /// <summary>
    /// Title of a report. Axis-bearing reports are named "[vertical] vs [horizontal]", matching how the chart itself is read.
    /// </summary>
    public static string Title(this ReportKind report) => report switch
    {
        ReportKind.PresentingOrderTimeline => "Presenting Order vs Date",
        ReportKind.StandupActivity => "Standup Activity",
        _ => report.ToString(),
    };

    /// <summary>One line describing what the report answers, shown beneath its title.</summary>
    public static string Description(this ReportKind report) => report switch
    {
        ReportKind.PresentingOrderTimeline => $"The turn each person took at every standup in the period. Anyone who did not present on a day drops to the {PresentationOrder.NoTurnSymbol} row.",
        ReportKind.StandupActivity => "Every day in the period the standup actually ran, newest first, with how many people presented and how long it took.",
        _ => string.Empty,
    };

    /// <summary>Shown in place of the report when the period produced nothing to show.</summary>
    public static string EmptyText(this ReportKind report) => report switch
    {
        ReportKind.StandupActivity => "This standup did not run on any day in the selected period.",
        _ => "Nobody presented at this standup over the selected period.",
    };
}
