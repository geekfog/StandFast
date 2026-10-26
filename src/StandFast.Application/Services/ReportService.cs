using StandFast.Application.Dtos;
using StandFast.Application.Mapping;
using StandFast.Application.Reporting;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

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

    public async Task<AttendanceDto> GetAttendanceAsync(Guid standupId, int days, TimeZoneInfo timeZone, CancellationToken cancellationToken = default)
    {
        (DateOnly from, DateOnly to) = ReportWindow.RangeEndingOn(clock.Today(timeZone), ReportWindow.ResolveDays(days));

        Standup? standup = await standups.GetAsync(standupId, cancellationToken);
        if (standup is null)
        {
            return AttendanceDto.Empty(standupId, string.Empty, from, to);
        }

        IReadOnlyList<Presentation> presentations = await entries.GetPresentationsAsync(standupId, from, to, cancellationToken);
        if (presentations.Count == 0)
        {
            return AttendanceDto.Empty(standup.Id, standup.Name, from, to);
        }

        Task<IReadOnlyList<StandupMember>> membersTask = standups.GetMembersAsync(standupId, RosterRole.Presenter, cancellationToken);
        Task<IReadOnlyList<Person>> peopleTask = people.GetAllAsync(cancellationToken);
        Task<IReadOnlyList<StandupMeeting>> meetingsTask = standups.GetMeetingsAsync(standupId, from, to, cancellationToken);
        await Task.WhenAll(membersTask, peopleTask, meetingsTask);

        Dictionary<Guid, Person> peopleById = peopleTask.Result.ToDictionary(person => person.Id);
        Dictionary<DateOnly, DateTimeOffset?> lockedByDate = meetingsTask.Result.ToDictionary(meeting => meeting.MeetingDate, meeting => meeting.LockedUtc);

        // The same people the board lists as presenters, keyed to the date they joined the roster, which is when they start being expected.
        Dictionary<Guid, DateOnly> joinedByPerson = membersTask.Result
            .Where(member => member.IsActive && peopleById.TryGetValue(member.PersonId, out Person? person) && person.IsActive)
            .ToDictionary(member => member.PersonId, member => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(member.CreatedUtc, timeZone).DateTime));

        IReadOnlyList<DateOnly> meetingDates = [.. presentations.Select(presentation => presentation.MeetingDate).Distinct()];
        ILookup<Guid, Presentation> turnsByPerson = presentations.ToLookup(presentation => presentation.PersonId);
        ILookup<Guid, TimeSpan> lengthsByPerson = presentations
            .GroupBy(presentation => presentation.MeetingDate)
            .SelectMany(meeting => TurnLengths(meeting, lockedByDate.GetValueOrDefault(meeting.Key)))
            .ToLookup(turn => turn.PersonId, turn => turn.Length);

        IReadOnlyList<PersonAttendanceDto> rows =
        [
            .. joinedByPerson.Keys
                .Union(turnsByPerson.Select(turns => turns.Key))
                .Select(personId =>
                {
                    HashSet<DateOnly> presentedDates = [.. turnsByPerson[personId].Select(presentation => presentation.MeetingDate)];
                    int? missed = joinedByPerson.TryGetValue(personId, out DateOnly joined)
                        ? meetingDates.Count(date => date >= joined && !presentedDates.Contains(date))
                        : null;

                    return new PersonAttendanceDto(
                        personId,
                        peopleById.TryGetValue(personId, out Person? person) ? person.DisplayName : UnknownPresenterName,
                        presentedDates.Count,
                        missed,
                        [.. lengthsByPerson[personId]]);
                })
                .OrderBy(row => row.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.PersonId),
        ];

        return new AttendanceDto(standup.Id, standup.Name, from, to, meetingDates.Count, rows);
    }

    public async Task<OrgChartDto> GetOrgChartAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Person> everyone = await people.GetAllAsync(cancellationToken);
        Dictionary<Guid, Person> peopleById = everyone.ToDictionary(person => person.Id);

        // Everyone active, and everyone above them, so an inactive supervisor keeps their active reports attached. The walk stops at anyone already shown.
        HashSet<Guid> shown = [];
        foreach (Person person in everyone.Where(person => person.IsActive))
        {
            for (Person? current = person; current is not null && shown.Add(current.Id); current = current.SupervisorId is { } supervisorId ? peopleById.GetValueOrDefault(supervisorId) : null)
            {
            }
        }

        ILookup<Guid?, Person> reportsOf = shown
            .Select(id => peopleById[id])
            .ToLookup(person => person.SupervisorId is { } supervisorId && shown.Contains(supervisorId) ? supervisorId : (Guid?)null);

        HashSet<Guid> placed = [];
        List<OrgChartNodeDto> roots = [.. ByName(reportsOf[null]).Select(Place)];

        // Saving never lets supervisors loop, but a restored backup could hold a loop. A loop has no top, so its first member by name starts a tree.
        foreach (Person person in ByName(shown.Select(id => peopleById[id])).Where(person => !placed.Contains(person.Id)))
        {
            roots.Add(Place(person));
        }

        return new OrgChartDto(roots, placed.Count);

        OrgChartNodeDto Place(Person person)
        {
            placed.Add(person.Id);
            return new OrgChartNodeDto(person.ToDto(), [.. ByName(reportsOf[person.Id]).Where(report => !placed.Contains(report.Id)).Select(Place)]);
        }

        static IEnumerable<Person> ByName(IEnumerable<Person> group) => group.OrderBy(person => person.DisplayName, StringComparer.CurrentCultureIgnoreCase).ThenBy(person => person.Id);
    }

    public async Task<LastPresentedDto> GetLastPresentedAsync(TimeZoneInfo fallbackTimeZone, CancellationToken cancellationToken = default)
    {
        Task<IReadOnlyList<Standup>> standupTask = standups.GetAllAsync(cancellationToken);
        Task<IReadOnlyList<Person>> peopleTask = people.GetAllAsync(cancellationToken);
        await Task.WhenAll(standupTask, peopleTask);

        Dictionary<Guid, Person> activePeople = peopleTask.Result.Where(person => person.IsActive).ToDictionary(person => person.Id);

        LastPresentedRowDto[][] perStandup = await Task.WhenAll(standupTask.Result.Where(standup => standup.IsActive).Select(async standup =>
        {
            DateOnly today = clock.Today(TimeZoneIds.Resolve(standup.TimeZoneId) ?? fallbackTimeZone);
            Task<IReadOnlyList<StandupMember>> membersTask = standups.GetMembersAsync(standup.Id, RosterRole.Presenter, cancellationToken);

            // The whole history up to today, since a long gap is exactly what this report is for and could reach back past any report period.
            Task<IReadOnlyList<Presentation>> presentationsTask = entries.GetPresentationsAsync(standup.Id, DateOnly.MinValue, today, cancellationToken);
            await Task.WhenAll(membersTask, presentationsTask);

            Dictionary<Guid, DateOnly> lastByPerson = presentationsTask.Result
                .GroupBy(presentation => presentation.PersonId)
                .ToDictionary(turns => turns.Key, turns => turns.Max(presentation => presentation.MeetingDate));

            return membersTask.Result
                .Where(member => member.IsActive && activePeople.ContainsKey(member.PersonId))
                .Select(member =>
                {
                    DateOnly? last = lastByPerson.TryGetValue(member.PersonId, out DateOnly date) ? date : null;
                    return new LastPresentedRowDto(standup.Id, standup.Name, member.PersonId, activePeople[member.PersonId].DisplayName, last, today.DayNumber - last?.DayNumber);
                })
                .ToArray();
        }));

        return new LastPresentedDto(
        [
            .. perStandup.SelectMany(rows => rows)
                .OrderByDescending(row => row.DaysSince ?? int.MaxValue)
                .ThenBy(row => row.StandupName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.DisplayName, StringComparer.CurrentCultureIgnoreCase),
        ]);
    }

    /// <summary>
    /// How long each turn of one meeting ran: from its start to the next turn's start, and for the last turn, to the lock. The last turn of a
    /// date still open has no end yet and is left out.
    /// </summary>
    private static IEnumerable<(Guid PersonId, TimeSpan Length)> TurnLengths(IEnumerable<Presentation> meeting, DateTimeOffset? lockedUtc)
    {
        Presentation[] turns = [.. meeting.OrderBy(presentation => presentation.PresentedUtc)];

        for (int index = 0; index < turns.Length; index++)
        {
            DateTimeOffset? endUtc = index + 1 < turns.Length ? turns[index + 1].PresentedUtc : lockedUtc;
            if (endUtc is { } end && end > turns[index].PresentedUtc)
            {
                yield return (turns[index].PersonId, end - turns[index].PresentedUtc);
            }
        }
    }
}
