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
        await Task.WhenAll(presenterTask, leaderTask, peopleTask, meetingTask);

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

        IReadOnlyList<BoardParticipantDto> participants = loaded
            .Select(item => item.Pair.ToParticipantDto(item.Member, item.Person))
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

        (StandupMember Member, Person Person)? context = await ResolveParticipantAsync(update.StandupId, update.PersonId, cancellationToken);
        if (context is null)
        {
            return null;
        }

        StandupEntryPair pair = await entries.GetCurrentAndPriorAsync(update.StandupId, update.PersonId, update.MeetingDate, cancellationToken);
        StandupEntry entry = pair.EnsureEntry(update.StandupId, update.PersonId, update.MeetingDate);

        entry.Update = Normalise(update.Update);
        entry.Blockers = Normalise(update.Blockers);
        entry.UpdateSavedUtc = clock.UtcNow;

        await entries.UpsertAsync(entry, cancellationToken);
        audit.Record(AuditEvents.UpdateSaved, AuditTargetType, EntryTargetId(update.StandupId, update.PersonId, update.MeetingDate), $"Update saved for {context.Value.Person.DisplayName}.");

        return new StandupEntryPair(entry, pair.Prior).ToParticipantDto(context.Value.Member, context.Value.Person);
    }

    public async Task<bool> SetLeaderAsync(Guid standupId, DateOnly meetingDate, Guid? personId, CancellationToken cancellationToken = default)
    {
        Person? leader = personId is { } id ? await ResolveLeaderAsync(standupId, id, cancellationToken) : null;
        if (personId is not null && leader is null)
        {
            return false;
        }

        StandupMeeting? existing = await standups.GetMeetingAsync(standupId, meetingDate, cancellationToken);
        GuardUnlocked(standupId, meetingDate, existing);

        await SaveMeetingAsync(standupId, meetingDate, existing, meeting => meeting.LeaderPersonId = personId, cancellationToken);
        audit.Record(AuditEvents.LeaderChanged, MeetingAuditTargetType, MeetingTargetId(standupId, meetingDate), leader is null ? "Leader cleared." : $"{leader.DisplayName} is leading.");

        return true;
    }

    public Task<IReadOnlyCollection<DateOnly>> GetPresentedDatesAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        entries.GetPresentedDatesAsync(standupId, from, to, cancellationToken);

    public async Task<IReadOnlyCollection<DateOnly>> GetLockedDatesAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        (await standups.GetMeetingsAsync(standupId, from, to, cancellationToken)).Where(meeting => meeting.IsLocked).Select(meeting => meeting.MeetingDate).ToHashSet();

    private async Task<BoardParticipantDto?> ChangeStateAsync(Guid standupId, DateOnly meetingDate, Guid personId, Func<AttendanceState, AttendanceState> transition, CancellationToken cancellationToken)
    {
        await GuardUnlockedAsync(standupId, meetingDate, cancellationToken);

        (StandupMember Member, Person Person)? context = await ResolveParticipantAsync(standupId, personId, cancellationToken);
        if (context is null)
        {
            return null;
        }

        StandupEntryPair pair = await entries.GetCurrentAndPriorAsync(standupId, personId, meetingDate, cancellationToken);
        StandupEntry entry = pair.EnsureEntry(standupId, personId, meetingDate);

        AttendanceState previous = entry.State;
        AttendanceState next = transition(previous);
        if (next == previous)
        {
            return new StandupEntryPair(entry, pair.Prior).ToParticipantDto(context.Value.Member, context.Value.Person);
        }

        entry.State = next;
        entry.MarkedAvailableUtc = next >= AttendanceState.Available ? entry.MarkedAvailableUtc ?? clock.UtcNow : null;
        entry.PresentedUtc = next == AttendanceState.Presented ? clock.UtcNow : null;

        await entries.UpsertAsync(entry, cancellationToken);
        audit.Record(AuditEvents.AttendanceChanged, AuditTargetType, EntryTargetId(standupId, personId, meetingDate), $"{context.Value.Person.DisplayName}: {previous} to {next}.");

        return new StandupEntryPair(entry, pair.Prior).ToParticipantDto(context.Value.Member, context.Value.Person);
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

    private async Task<(StandupMember Member, Person Person)?> ResolveParticipantAsync(Guid standupId, Guid personId, CancellationToken cancellationToken)
    {
        StandupMember? member = await FindActiveMemberAsync(standupId, personId, RosterRole.Presenter, cancellationToken);
        if (member is null)
        {
            return null;
        }

        Person? person = await people.GetAsync(personId, cancellationToken);
        return person is null ? null : (member, person);
    }

    /// <summary>The person behind a leader pick, or null when they are not on the standup's leader roster or their person record has gone.</summary>
    private async Task<Person?> ResolveLeaderAsync(Guid standupId, Guid personId, CancellationToken cancellationToken) =>
        await FindActiveMemberAsync(standupId, personId, RosterRole.Leader, cancellationToken) is null ? null : await people.GetAsync(personId, cancellationToken);

    private async Task<StandupMember?> FindActiveMemberAsync(Guid standupId, Guid personId, RosterRole role, CancellationToken cancellationToken) =>
        (await standups.GetMembersAsync(standupId, role, cancellationToken)).FirstOrDefault(candidate => candidate.PersonId == personId && candidate.IsActive);

    private static string? Normalise(string? markdown) => string.IsNullOrWhiteSpace(markdown) ? null : markdown.Trim();

    private static string EntryTargetId(Guid standupId, Guid personId, DateOnly meetingDate) => $"{standupId}/{personId}/{MeetingCalendar.ToRouteValue(meetingDate)}";

    private static string MeetingTargetId(Guid standupId, DateOnly meetingDate) => $"{standupId}/{MeetingCalendar.ToRouteValue(meetingDate)}";
}
