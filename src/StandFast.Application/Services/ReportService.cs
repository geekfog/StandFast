using StandFast.Application.Dtos;
using StandFast.Application.Reporting;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;

namespace StandFast.Application.Services;

public sealed class ReportService(IStandupRepository standups, IPersonRepository people, IStandupEntryRepository entries, IClock clock) : IReportService
{
    /// <summary>Shown for a turn whose person record has since been deleted, so the history stays complete rather than dropping the line.</summary>
    private const string UnknownPresenterName = "Removed person";

    public async Task<PresentingOrderTimelineDto> GetPresentingOrderTimelineAsync(Guid standupId, int days, TimeZoneInfo timeZone, CancellationToken cancellationToken = default)
    {
        (DateOnly from, DateOnly to) = ReportWindow.RangeEndingOn(clock.Today(timeZone), ReportWindow.ResolveDays(days));

        Standup? standup = await standups.GetAsync(standupId, cancellationToken);
        if (standup is null)
        {
            return PresentingOrderTimelineDto.Empty(standupId, string.Empty, from, to);
        }

        IReadOnlyList<Presentation> presentations = await entries.GetPresentationsAsync(standupId, from, to, cancellationToken);
        if (presentations.Count == 0)
        {
            return PresentingOrderTimelineDto.Empty(standup.Id, standup.Name, from, to);
        }

        // A meeting's turns are ranked among themselves, so the order is worked out per date and then read back per person.
        IReadOnlyList<DateOnly> meetingDates = [.. presentations.Select(presentation => presentation.MeetingDate).Distinct().Order()];
        Dictionary<DateOnly, IReadOnlyDictionary<Guid, int>> ordersByDate = presentations
            .GroupBy(presentation => presentation.MeetingDate)
            .ToDictionary(meeting => meeting.Key, PresentationOrder.WithinMeeting);

        // Roster membership is deliberately not consulted: the report is of who actually presented, which includes anyone since taken off the roster.
        Dictionary<Guid, Person> peopleById = (await people.GetAllAsync(cancellationToken)).ToDictionary(person => person.Id);

        IReadOnlyList<PresenterTimelineDto> presenters =
        [
            .. presentations
                .Select(presentation => presentation.PersonId)
                .Distinct()
                .Select(personId => new PresenterTimelineDto(
                    personId,
                    peopleById.TryGetValue(personId, out Person? person) ? person.DisplayName : UnknownPresenterName,
                    [.. meetingDates.Select(date => ordersByDate[date].TryGetValue(personId, out int order) ? order : (int?)null)]))
                .OrderBy(presenter => presenter.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(presenter => presenter.PersonId),
        ];

        int highestOrder = ordersByDate.Values.Max(meeting => meeting.Count);

        return new PresentingOrderTimelineDto(standup.Id, standup.Name, from, to, meetingDates, presenters, highestOrder);
    }

    public async Task<StandupActivityDto> GetStandupActivityAsync(Guid standupId, int days, TimeZoneInfo timeZone, CancellationToken cancellationToken = default)
    {
        (DateOnly from, DateOnly to) = ReportWindow.RangeEndingOn(clock.Today(timeZone), ReportWindow.ResolveDays(days));

        Standup? standup = await standups.GetAsync(standupId, cancellationToken);
        if (standup is null)
        {
            return StandupActivityDto.Empty(standupId, string.Empty, from, to);
        }

        IReadOnlyList<Presentation> presentations = await entries.GetPresentationsAsync(standupId, from, to, cancellationToken);
        if (presentations.Count == 0)
        {
            return StandupActivityDto.Empty(standup.Id, standup.Name, from, to);
        }

        // A turn is what makes a date part of the report, so the meeting rows are only consulted for the lock: a date annotated but never presented on did not run.
        Dictionary<DateOnly, StandupMeeting> meetingsByDate = (await standups.GetMeetingsAsync(standupId, from, to, cancellationToken))
            .ToDictionary(meeting => meeting.MeetingDate);

        IReadOnlyList<StandupActivityDayDto> activity =
        [
            .. presentations
                .GroupBy(presentation => presentation.MeetingDate)
                .OrderByDescending(day => day.Key)
                .Select(day => new StandupActivityDayDto(
                    day.Key,
                    meetingsByDate.TryGetValue(day.Key, out StandupMeeting? meeting) ? meeting.LockedUtc : null,
                    day.Select(presentation => presentation.PersonId).Distinct().Count(),
                    day.Min(presentation => presentation.PresentedUtc))),
        ];

        return new StandupActivityDto(standup.Id, standup.Name, from, to, activity);
    }
}
