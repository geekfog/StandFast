using FluentValidation;
using Microsoft.Extensions.Options;
using StandFast.Application.Auditing;
using StandFast.Application.Configuration;
using StandFast.Application.Dtos;
using StandFast.Application.Mapping;
using StandFast.Application.Scheduling;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Services;

public sealed class BoardService(
    IStandupRepository standups,
    IPersonRepository people,
    IStandupEntryRepository entries,
    IHolidayRepository holidays,
    IClock clock,
    IValidator<ParticipantUpdateDto> validator,
    IOptions<BoardLockOptions> lockOptions,
    IAuditLog audit) : IBoardService
{
    private const string AuditTargetType = nameof(StandupEntry);
    private const string MeetingAuditTargetType = nameof(StandupMeeting);

    /// <summary>How someone can take part, in the order their rosters win when a person is on more than one.</summary>
    private static readonly AttendeeKind[] RosterPriority = [AttendeeKind.Presenter, AttendeeKind.Leader, AttendeeKind.Guest];

    public async Task<StandupBoardDto> GetBoardAsync(Guid standupId, DateOnly meetingDate, CancellationToken cancellationToken = default)
    {
        Standup? standup = await standups.GetAsync(standupId, cancellationToken);
        if (standup is null)
        {
            return StandupBoardDto.Empty(meetingDate);
        }

        Dictionary<RosterRole, Task<IReadOnlyList<StandupMember>>> memberTasks = RosterPriority.ToDictionary(kind => kind.Roster(), kind => standups.GetMembersAsync(standupId, kind.Roster(), cancellationToken));
        Task<IReadOnlyList<Person>> peopleTask = people.GetAllAsync(cancellationToken);
        Task<StandupMeeting?> meetingTask = standups.GetMeetingAsync(standupId, meetingDate, cancellationToken);
        Task<IReadOnlyList<StandupEntry>> onDateTask = entries.GetOnDateAsync(standupId, meetingDate, cancellationToken);
        Task<IReadOnlyList<Sprint>> sprintTask = standups.GetSprintsAsync(standupId, cancellationToken);
        Task<IReadOnlyList<Holiday>> holidayTask = holidays.GetAllAsync(cancellationToken);
        await Task.WhenAll([.. memberTasks.Values, peopleTask, meetingTask, onDateTask, sprintTask, holidayTask]);

        // How far days off reach depends on the sprint, so they are read once the sprint is known.
        IReadOnlyDictionary<DateOnly, HolidayDuration> holidayDurations = holidayTask.Result.ToDurationByDate();
        SprintStatusDto? sprint = BoardAvailability.ForSprint(sprintTask.Result, meetingDate, holidayDurations);
        IReadOnlyList<PersonDayOff> daysOff = await people.GetDaysOffAsync(BoardAvailability.Period(meetingDate, sprint), cancellationToken);

        Dictionary<Guid, Person> peopleById = peopleTask.Result.ToDictionary(person => person.Id);

        Dictionary<Guid, (AttendeeKind Kind, StandupMember Member)> rosterByPerson = [];
        foreach (AttendeeKind kind in RosterPriority)
        {
            foreach (StandupMember member in memberTasks[kind.Roster()].Result.Where(member => member.IsActive))
            {
                rosterByPerson.TryAdd(member.PersonId, (kind, member));
            }
        }

        (StandupMember Member, Person Person, StandupEntryPair Pair)[] loaded = await Task.WhenAll(rosterByPerson.Values
            .Where(roster => roster.Kind == AttendeeKind.Presenter && peopleById.ContainsKey(roster.Member.PersonId))
            .Select(roster => roster.Member)
            .Select(async member => (
                Member: member,
                Person: peopleById[member.PersonId],
                Pair: await entries.GetCurrentAndPriorAsync(standupId, member.PersonId, meetingDate, cancellationToken))));

        // Ranked across the whole roster, so it has to happen here rather than while mapping any single participant.
        IReadOnlyDictionary<Guid, int> priorTurns = PresentationOrder.AtMostRecentMeeting(
            loaded.Select(item => item.Pair.Prior?.CompletedTurn).OfType<Presentation>());

        // Everyone else on a roster is offered on it. Anyone on no roster shows only on a date they visited.
        HashSet<Guid> presenterIds = [.. loaded.Select(item => item.Person.Id)];
        Dictionary<Guid, StandupEntry> entriesOnDate = onDateTask.Result.ToDictionary(entry => entry.PersonId);
        IEnumerable<(Person Person, BoardParticipantDto Participant)> nonPresenters = peopleTask.Result
            .Where(person => !presenterIds.Contains(person.Id))
            .Select(person => (Person: person, IsRostered: rosterByPerson.TryGetValue(person.Id, out (AttendeeKind Kind, StandupMember Member) roster), Roster: roster))
            .Select(item => (item.Person, item.IsRostered, Kind: item.IsRostered ? item.Roster.Kind : AttendeeKind.Guest, DisplayOrder: item.IsRostered ? item.Roster.Member.DisplayOrder : BoardMappings.GuestDisplayOrder))
            .Select(item => (item.Person, item.IsRostered, Participant: new StandupEntryPair(entriesOnDate.GetValueOrDefault(item.Person.Id), null).ToParticipantDto(
                item.Person, item.Kind, standup.Presents(item.Kind), item.DisplayOrder)))
            .Where(item => AttendanceTransitions.IsNonPresenterPlacement(item.Participant.State, item.Participant.Presents) && (item.IsRostered || AttendanceTransitions.IsAttending(item.Participant.State)))
            .Select(item => (item.Person, item.Participant));

        IReadOnlyList<BoardParticipantDto> participants = loaded
            .Select(item => (item.Person, Participant: item.Pair.ToParticipantDto(item.Person, AttendeeKind.Presenter, presents: true, item.Member.DisplayOrder)))
            .Concat(nonPresenters)
            .Where(item => AttendanceTransitions.IsOnBoard(item.Participant.State, item.Person.IsActive))
            .Select(item => item.Participant)
            .InColumnOrder(AttendanceState.Roster);

        IReadOnlyList<StandupMemberDto> leaders = memberTasks[RosterRole.Leader].Result.Where(member => member.IsActive).ToRoster(peopleById);

        return new StandupBoardDto(
            standup.Id,
            standup.Name,
            meetingDate,
            standup.OccursOn(meetingDate),
            participants,
            leaders,
            meetingTask.Result?.LeaderPersonId,
            meetingTask.Result?.LockedUtc,
            priorTurns,
            sprint,
            BoardAvailability.ForPeople(daysOff, meetingDate, sprint, holidayDurations));
    }

    public async Task<DateTimeOffset?> LockAsync(Guid standupId, DateOnly meetingDate, CancellationToken cancellationToken = default)
    {
        StandupMeeting? existing = await standups.GetMeetingAsync(standupId, meetingDate, cancellationToken);
        if (existing?.LockedUtc is { } alreadyLocked)
        {
            return alreadyLocked;
        }

        IReadOnlyList<Presentation> turns = await entries.GetPresentationsAsync(standupId, meetingDate, meetingDate, cancellationToken);
        DateTimeOffset? lastTurnUtc = turns.Count == 0 ? null : turns.Max(turn => turn.PresentedUtc);
        BoardLockOptions options = lockOptions.Value;
        DateTimeOffset lockedUtc = BoardLockPolicy.LockedAt(clock.UtcNow, lastTurnUtc, options.Grace, options.TailAfterLastTurn);

        await SaveMeetingAsync(standupId, meetingDate, existing, meeting => meeting.LockedUtc = lockedUtc, cancellationToken);
        audit.Record(AuditEvents.BoardLocked, MeetingAuditTargetType, MeetingTargetId(standupId, meetingDate), $"Board locked as of {lockedUtc:u}.");

        return lockedUtc;
    }

    public async Task UnlockAsync(Guid standupId, DateOnly meetingDate, CancellationToken cancellationToken = default)
    {
        StandupMeeting? existing = await standups.GetMeetingAsync(standupId, meetingDate, cancellationToken);
        if (existing is null || !existing.IsLocked)
        {
            return;
        }

        await SaveMeetingAsync(standupId, meetingDate, existing, meeting => meeting.LockedUtc = null, cancellationToken);
        audit.Record(AuditEvents.BoardUnlocked, MeetingAuditTargetType, MeetingTargetId(standupId, meetingDate), "Board unlocked.");
    }

    public Task<BoardParticipantDto?> AdvanceAsync(Guid standupId, DateOnly meetingDate, Guid personId, CancellationToken cancellationToken = default) =>
        ChangeStateAsync(standupId, meetingDate, personId, (attendee, state) => AttendanceTransitions.Advance(state, attendee.Kind, attendee.Presents), cancellationToken);

    public Task<BoardParticipantDto?> RevertAsync(Guid standupId, DateOnly meetingDate, Guid personId, CancellationToken cancellationToken = default) =>
        ChangeStateAsync(standupId, meetingDate, personId, (attendee, state) => AttendanceTransitions.Revert(state, attendee.Kind), cancellationToken);

    public async Task<BoardParticipantDto?> SaveUpdateAsync(ParticipantUpdateDto update, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(update, cancellationToken);
        await GuardUnlockedAsync(update.StandupId, update.MeetingDate, cancellationToken);

        if (await ResolveAttendeeAsync(update.StandupId, update.PersonId, cancellationToken) is not { } attendee)
        {
            return null;
        }

        StandupEntryPair pair = await entries.GetCurrentAndPriorAsync(update.StandupId, update.PersonId, update.MeetingDate, cancellationToken);
        StandupEntry entry = pair.EnsureEntry(update.StandupId, update.PersonId, update.MeetingDate);
        if (!attendee.IsOnBoard(entry.State))
        {
            return null;
        }

        entry.Update = OptionalText.Normalize(update.Update);
        entry.Blockers = attendee.Presents ? OptionalText.Normalize(update.Blockers) : null;
        entry.ParkingLot = attendee.Presents ? OptionalText.Normalize(update.ParkingLot) : null;

        // Saving every box empty deletes the update. The entry row goes too unless it still records attendance.
        bool isDeleted = !entry.HasContent;
        entry.UpdateSavedUtc = isDeleted ? null : clock.UtcNow;

        if (entry.IsBlank)
        {
            await entries.DeleteAsync(update.StandupId, update.PersonId, update.MeetingDate, cancellationToken);
        }
        else
        {
            await entries.UpsertAsync(entry, cancellationToken);
        }

        audit.Record(
            isDeleted ? AuditEvents.UpdateDeleted : AuditEvents.UpdateSaved,
            AuditTargetType,
            EntryTargetId(update.StandupId, update.PersonId, update.MeetingDate),
            $"Update {(isDeleted ? "deleted" : "saved")} for {attendee.Person.DisplayName}.");

        return attendee.ToParticipantDto(new StandupEntryPair(entry, pair.Prior));
    }

    public async Task<bool> SetLeaderAsync(Guid standupId, DateOnly meetingDate, Guid? personId, CancellationToken cancellationToken = default)
    {
        StandupMeeting? existing = await standups.GetMeetingAsync(standupId, meetingDate, cancellationToken);
        GuardUnlocked(standupId, meetingDate, existing);

        Person? leader = personId is { } id ? await ResolveLeaderAsync(standupId, meetingDate, id, cancellationToken) : null;
        if (personId is not null && leader is null)
        {
            return false;
        }

        await SaveMeetingAsync(standupId, meetingDate, existing, meeting => meeting.LeaderPersonId = personId, cancellationToken);
        audit.Record(AuditEvents.LeaderChanged, MeetingAuditTargetType, MeetingTargetId(standupId, meetingDate), leader is null ? "Leader cleared." : $"{leader.DisplayName} is leading.");

        return true;
    }

    public Task<IReadOnlyCollection<DateOnly>> GetPresentedDatesAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        entries.GetPresentedDatesAsync(standupId, from, to, cancellationToken);

    public async Task<IReadOnlyCollection<DateOnly>> GetLockedDatesAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        (await standups.GetMeetingsAsync(standupId, from, to, cancellationToken)).Where(meeting => meeting.IsLocked).Select(meeting => meeting.MeetingDate).ToHashSet();

    private async Task<BoardParticipantDto?> ChangeStateAsync(
        Guid standupId, DateOnly meetingDate, Guid personId, Func<Attendee, AttendanceState, AttendanceState> transition, CancellationToken cancellationToken)
    {
        StandupMeeting? meeting = await standups.GetMeetingAsync(standupId, meetingDate, cancellationToken);
        GuardUnlocked(standupId, meetingDate, meeting);

        if (await ResolveAttendeeAsync(standupId, personId, cancellationToken) is not { } attendee)
        {
            return null;
        }

        StandupEntryPair pair = await entries.GetCurrentAndPriorAsync(standupId, personId, meetingDate, cancellationToken);
        StandupEntry entry = pair.EnsureEntry(standupId, personId, meetingDate);

        AttendanceState previous = AttendanceTransitions.Placement(entry.State, attendee.Kind);
        if (!attendee.IsOnBoard(entry.State))
        {
            return null;
        }

        AttendanceState next = transition(attendee, previous);
        if (next == previous)
        {
            return attendee.ToParticipantDto(new StandupEntryPair(entry, pair.Prior));
        }

        if (previous == AttendanceState.Roster && next == AttendanceState.Available
            && (await people.GetDaysOffAsync(personId, cancellationToken)).FirstOrDefault(dayOff => dayOff.Dates.Contains(meetingDate)) is { } dayOff)
        {
            throw new PersonOffException(attendee.Person.DisplayName, dayOff.Note);
        }

        bool attending = AttendanceTransitions.IsAttending(next);
        entry.State = next;
        entry.MarkedAvailableUtc = !attending ? null : AttendanceTransitions.IsAttending(previous) ? entry.MarkedAvailableUtc ?? clock.UtcNow : clock.UtcNow;
        entry.PresentedUtc = next == AttendanceState.Presented ? clock.UtcNow : null;

        await entries.UpsertAsync(entry, cancellationToken);
        audit.Record(AuditEvents.AttendanceChanged, AuditTargetType, EntryTargetId(standupId, personId, meetingDate), $"{attendee.Person.DisplayName}: {previous} to {next}.");

        // Only someone attending can lead, so taking the leader back off the board clears the pick.
        if (!attending && meeting?.LeaderPersonId == personId)
        {
            await SaveMeetingAsync(standupId, meetingDate, meeting, changed => changed.LeaderPersonId = null, cancellationToken);
            audit.Record(AuditEvents.LeaderChanged, MeetingAuditTargetType, MeetingTargetId(standupId, meetingDate), $"Leader cleared: {attendee.Person.DisplayName} is no longer attending.");
        }

        return attendee.ToParticipantDto(new StandupEntryPair(entry, pair.Prior));
    }

    /// <summary>Writes one change to the date's meeting row, creating the row when nothing has been recorded for that date yet.</summary>
    private async Task SaveMeetingAsync(Guid standupId, DateOnly meetingDate, StandupMeeting? existing, Action<StandupMeeting> change, CancellationToken cancellationToken)
    {
        StandupMeeting meeting = existing ?? new StandupMeeting { StandupId = standupId, MeetingDate = meetingDate, CreatedUtc = clock.UtcNow };
        change(meeting);
        meeting.ModifiedUtc = existing is null ? null : clock.UtcNow;

        await standups.UpsertMeetingAsync(meeting, cancellationToken);
    }

    /// <summary>Refuses a change to a locked date. The service is where the lock holds, so a board opened before the lock cannot write through a screen that still offers the controls.</summary>
    private async Task GuardUnlockedAsync(Guid standupId, DateOnly meetingDate, CancellationToken cancellationToken) =>
        GuardUnlocked(standupId, meetingDate, await standups.GetMeetingAsync(standupId, meetingDate, cancellationToken));

    private static void GuardUnlocked(Guid standupId, DateOnly meetingDate, StandupMeeting? meeting)
    {
        if (meeting?.LockedUtc is { } lockedUtc)
        {
            throw new BoardLockedException(standupId, meetingDate, lockedUtc);
        }
    }

    /// <summary>
    /// How a person takes part in the standup: the first of its presenter, leader, and guest rosters they are on, or a visiting guest when they are on none.
    /// Null when their person record or the standup has gone.
    /// Whether an inactive person can be changed on a given date is <see cref="Attendee.IsOnBoard"/>.
    /// </summary>
    private async Task<Attendee?> ResolveAttendeeAsync(Guid standupId, Guid personId, CancellationToken cancellationToken)
    {
        Task<Person?> personTask = people.GetAsync(personId, cancellationToken);
        Task<Standup?> standupTask = standups.GetAsync(standupId, cancellationToken);
        if (await personTask is not { } person || await standupTask is not { } standup)
        {
            return null;
        }

        foreach (AttendeeKind kind in RosterPriority)
        {
            if (await FindActiveMemberAsync(standupId, personId, kind.Roster(), cancellationToken) is { } member)
            {
                return new Attendee(person, kind, standup.Presents(kind), member.DisplayOrder);
            }
        }

        return new Attendee(person, AttendeeKind.Guest, standup.Presents(AttendeeKind.Guest), BoardMappings.GuestDisplayOrder);
    }

    /// <summary>The person behind a leader pick, or null when they are not on the standup's leader roster, are not attending this date, or their person record has gone.</summary>
    private async Task<Person?> ResolveLeaderAsync(Guid standupId, DateOnly meetingDate, Guid personId, CancellationToken cancellationToken)
    {
        if (await FindActiveMemberAsync(standupId, personId, RosterRole.Leader, cancellationToken) is null
            || await ResolveAttendeeAsync(standupId, personId, cancellationToken) is not { } attendee)
        {
            return null;
        }

        StandupEntryPair pair = await entries.GetCurrentAndPriorAsync(standupId, personId, meetingDate, cancellationToken);
        AttendanceState state = AttendanceTransitions.Placement(pair.Current?.State ?? AttendanceState.Roster, attendee.Kind);

        return AttendanceTransitions.IsAttending(state) ? attendee.Person : null;
    }

    private async Task<StandupMember?> FindActiveMemberAsync(Guid standupId, Guid personId, RosterRole role, CancellationToken cancellationToken) =>
        (await standups.GetMembersAsync(standupId, role, cancellationToken)).FirstOrDefault(candidate => candidate.PersonId == personId && candidate.IsActive);

    private static string EntryTargetId(Guid standupId, Guid personId, DateOnly meetingDate) => $"{standupId}/{personId}/{MeetingCalendar.ToRouteValue(meetingDate)}";

    private static string MeetingTargetId(Guid standupId, DateOnly meetingDate) => $"{standupId}/{MeetingCalendar.ToRouteValue(meetingDate)}";

    /// <summary>A person resolved against one standup, carrying what a board tile needs beyond their entries.</summary>
    private sealed record Attendee(Person Person, AttendeeKind Kind, bool Presents, int DisplayOrder)
    {
        public BoardParticipantDto ToParticipantDto(StandupEntryPair pair) => pair.ToParticipantDto(Person, Kind, Presents, DisplayOrder);

        /// <summary>The board's own visibility rule, so nothing can change someone the board does not show, such as an inactive person not attending.</summary>
        public bool IsOnBoard(AttendanceState stored) => AttendanceTransitions.IsOnBoard(AttendanceTransitions.Placement(stored, Kind), Person.IsActive);
    }
}
