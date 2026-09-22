using StandFast.Application.Dtos;

namespace StandFast.Application.Services;

public interface IReportService
{
    /// <summary>
    /// The presenting order report for one standup over the last <paramref name="days"/> days, ending today in <paramref name="timeZone"/>.
    /// </summary>
    Task<PresentingOrderTimelineDto> GetPresentingOrderTimelineAsync(Guid standupId, int days, TimeZoneInfo timeZone, CancellationToken cancellationToken = default);
}
