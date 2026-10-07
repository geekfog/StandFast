using FluentValidation;
using Microsoft.Extensions.Options;
using StandFast.Application.Auditing;
using StandFast.Application.Configuration;
using StandFast.Application.Dtos;
using StandFast.Application.Mapping;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Services;

public sealed class BoardService(
    IStandupRepository standups,
    IPersonRepository people,
    IStandupEntryRepository entries,
    IClock clock,
    IValidator<ParticipantUpdateDto> validator,
    IOptions<BoardLockOptions> lockOptions,
    IAuditLog audit) : IBoardService
{
    private const string AuditTargetType = nameof(StandupEntry);
    private const string MeetingAuditTargetType = nameof(StandupMeeting);

    public async Task<StandupBoardDto> GetBoardAsync(Guid standupId, DateOnly meetingDate, CancellationToken cancellationToken = default)
    {
        Standup? standup = await standups.GetAsync(standupId, cancellationToken);
        if (standup is null)
        {
            return StandupBoardDto.Empty(meetingDate);
        }

        Task<IReadOnlyList<StandupMember>> presenterTask = standups.GetMembersAsync(standupId, RosterRole.Presenter, cancellationToken);
        Task<IReadOnlyList<StandupMember>> leaderTask = standups.GetMembersAsync(standupId, RosterRole.Leader, cancellationToken);
        Task<IReadOnlyList<Person>> peopleTask = people.GetAllAsync(cancellationToken);
        Task<StandupMeeting?> meetingTask = standups.GetMeetingAsync(standupId, meetingDate, cancellationToken);
        Task<IReadOnlyList<StandupEntry>> onDateTask = entries.GetOnDateAsync(standupId, meetingDate, cancellationToken);
        await Task.WhenAll(presenterTask, leaderTask, peopleTask, meetingTask, onDateTask);

        Dictionary<Guid, Person> peopleById = peopleTask.Result.ToDictionary(person => person.Id);

        (StandupMember Member, Person Person, StandupEntryPair Pair)[] loaded = await Task.WhenAll(presenterTask.Result
            .Where(member => member.IsActive && peopleById.ContainsKey(member.PersonId))
            .Select(async member => (
                Member: member,
                Person: peopleById[member.PersonId],
                Pair: await entries.GetCurrentAndPriorAsync(standupId, member.PersonId, meetingDate, cancellationToken))));

        // Ranked across the whole roster, so it has to happen here rather than while mapping any single participant.
        IReadOnlyDictionary<Guid, int> priorTurns = PresentationOrder.AtMostRecentMeeting(
            loaded.Select(item => item.Pair.Prior?.CompletedTurn).OfType<Presentation>());

        // Anyone active who is not presenting can visit, so guests come from the whole directory rather than a roster.
        HashSet<Guid> presenterIds = [.. loaded.Select(item => item.Person.Id)];
        Dictionary<Guid, StandupEntry> entriesOnDate = onDateTask.Result.ToDictionary(entry => entry.PersonId);
        IEnumerable<BoardParticipantDto> guests = peopleTask.Result
            .Where(person => person.IsActive && !presenterIds.Contains(person.Id))
            .Select(person => new StandupEntryPair(entriesOnDate.GetValueOrDefault(person.Id), null).ToParticipantDto(person, AttendeeKind.Guest, BoardMappings.GuestDisplayOrder))
            .Where(guest => AttendanceTransitions.IsGuestPlacement(guest.State));

        IReadOnlyList<BoardParticipantDto> participants = loaded
            .Select(item => item.Pair.ToParticipantDto(item.Person, AttendeeKind.Presenter, item.Member.DisplayOrder))
            .Concat(guests)
            .InColumnOrder(AttendanceState.Roster);

        IReadOnlyList<StandupMemberDto> leaders = leaderTask.Result.Where(member => member.IsActive).ToRoster(peopleById);

        return new StandupBoardDto(
            standup.Id,
            standup.Name,
            meetingDate,
            standup.OccursOn(meetingDate),
            participants,
            leaders,
            meetingTask.Result?.LeaderPersonId,
            meetingTask.Result?.LockedUtc,
            priorTurns);
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
        ChangeStateAsync(standupId, meetingDate, personId, AttendanceTransitions.Advance, cancellationToken);

    public Task<BoardParticipantDto?> RevertAsync(Guid standupId, DateOnly meetingDate, Guid personId, CancellationToken cancellationToken = default) =>
        ChangeStateAsync(standupId, meetingDate, personId, AttendanceTransitions.Revert, cancellationToken);

    public async Task<BoardParticipantDto?> SaveUpdateAsync(ParticipantUpdateDto update, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(update, cancellationToken);
        await GuardUnlockedAsync(update.StandupId, update.MeetingDate, cancellationToken);

        // Guests attend without presenting, so only a presenter has an update to record.
        if (await ResolveAttendeeAsync(update.StandupId, update.PersonId, cancellationToken) is not { Kind: AttendeeKind.Presenter } attendee)
        {
            return null;
        }

        StandupEntryPair pair = await entries.GetCurrentAndPriorAsync(update.StandupId, update.PersonId, update.MeetingDate, cancellationToken);
        StandupEntry entry = pair.EnsureEntry(update.StandupId, update.PersonId, update.MeetingDate);

        entry.Update = OptionalText.Normalize(update.Update);
        entry.Blockers = OptionalText.Normalize(update.Blockers);
        entry.ParkingLot = OptionalText.Normalize(update.ParkingLot);
        entry.UpdateSavedUtc = clock.UtcNow;

        await entries.UpsertAsync(entry, cancellationToken);
        audit.Record(AuditEvents.UpdateSaved, AuditTargetType, EntryTargetId(update.StandupId, update.PersonId, update.MeetingDate), $"Update saved for {attendee.Person.DisplayName}.");

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
        Guid standupId, DateOnly meetingDate, Guid personId, Func<AttendanceState, AttendeeKind, AttendanceState> transition, CancellationToken cancellationToken)
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
        AttendanceState next = transition(previous, attendee.Kind);
        if (next == previous)
        {
            return attendee.ToParticipantDto(new StandupEntryPair(entry, pair.Prior));
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
    /// How a person takes part in the standup: a presenter when they are on its presenter roster, otherwise a guest as long as they are active.
    /// Null when their person record has gone, or they are inactive and not presenting.
    /// </summary>
    private async Task<Attendee?> ResolveAttendeeAsync(Guid standupId, Guid personId, CancellationToken cancellationToken)
    {
        Person? person = await people.GetAsync(personId, cancellationToken);
        if (person is null)
        {
            return null;
        }

        if (await FindActiveMemberAsync(standupId, personId, RosterRole.Presenter, cancellationToken) is { } member)
        {
            return new Attendee(person, AttendeeKind.Presenter, member.DisplayOrder);
        }

        return person.IsActive ? new Attendee(person, AttendeeKind.Guest, BoardMappings.GuestDisplayOrder) : null;
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
    private sealed record Attendee(Person Person, AttendeeKind Kind, int DisplayOrder)
    {
        public BoardParticipantDto ToParticipantDto(StandupEntryPair pair) => pair.ToParticipantDto(Person, Kind, DisplayOrder);
    }
}
