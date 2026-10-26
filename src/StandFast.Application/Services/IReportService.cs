using StandFast.Application.Dtos;

namespace StandFast.Application.Services;

public interface IReportService
{
    /// <summary>
    /// The presenting order report for one standup over the last <paramref name="days"/> days, ending today in <paramref name="timeZone"/>.
    /// </summary>
    Task<PresentingOrderTimelineDto> GetPresentingOrderTimelineAsync(Guid standupId, int days, TimeZoneInfo timeZone, CancellationToken cancellationToken = default);

    /// <summary>
    /// The activity report for one standup over the last <paramref name="days"/> days, ending today in <paramref name="timeZone"/>: one row per
    /// date somebody presented on, newest first.
    /// </summary>
    Task<StandupActivityDto> GetStandupActivityAsync(Guid standupId, int days, TimeZoneInfo timeZone, CancellationToken cancellationToken = default);

    /// <summary>
    /// The attendance report for one standup over the last <paramref name="days"/> days, ending today in <paramref name="timeZone"/>: one row
    /// per person on the presenter roster or who presented in the period, sorted by name.
    /// </summary>
    Task<AttendanceDto> GetAttendanceAsync(Guid standupId, int days, TimeZoneInfo timeZone, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everyone active in People arranged under their supervisors, plus any inactive person someone active reports up through, so no chain is broken.
    /// A person whose supervisor is not shown starts a tree of their own.
    /// </summary>
    Task<OrgChartDto> GetOrgChartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Every active person on the presenter roster of every active standup, with the last date they presented there and how many days ago that was.
    /// Those who never presented come first, then the longest gap. "Today" is in each standup's own time zone, or <paramref name="fallbackTimeZone"/> when it cannot be resolved.
    /// </summary>
    Task<LastPresentedDto> GetLastPresentedAsync(TimeZoneInfo fallbackTimeZone, CancellationToken cancellationToken = default);
}
