namespace StandFast.Application.Reporting;

/// <summary>
/// A "last N days" span a report can be run over. The quick-pick buttons, the default span, and the bounds checking on a span supplied in a
/// query string all read from <see cref="All"/>, so the set of offered spans is defined once.
/// </summary>
/// <param name="Label">Button text, phrased the way the span is spoken rather than always in days.</param>
public sealed record ReportWindow(string Label, int Days)
{
    public static readonly ReportWindow OneMonth = new("30 days", 30);
    public static readonly ReportWindow TwoMonths = new("60 days", 60);
    public static readonly ReportWindow OneQuarter = new("90 days", 90);
    public static readonly ReportWindow HalfYear = new("180 days", 180);
    public static readonly ReportWindow OneYear = new("1 year", 365);

    /// <summary>Every offered span, shortest first. Quick-pick buttons render in this order.</summary>
    public static readonly IReadOnlyList<ReportWindow> All = [OneMonth, TwoMonths, OneQuarter, HalfYear, OneYear];

    /// <summary>The span a report opens on when none was asked for.</summary>
    public static ReportWindow Default => OneMonth;

    /// <summary>The shortest and longest offered spans, which bound any day count arriving from a query string.</summary>
    public static int MinimumDays => All.Min(window => window.Days);

    public static int MaximumDays => All.Max(window => window.Days);

    /// <summary>The day count to report over, clamped to the offered range. Falls back to <see cref="Default"/> when nothing was asked for.</summary>
    public static int ResolveDays(int? requestedDays) =>
        requestedDays is null ? Default.Days : Math.Clamp(requestedDays.Value, MinimumDays, MaximumDays);

    /// <summary>The inclusive date range a span covers, counting <paramref name="today"/> as the last of its days.</summary>
    public static (DateOnly From, DateOnly To) RangeEndingOn(DateOnly today, int days) => (today.AddDays(-(days - 1)), today);
}
