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
}
