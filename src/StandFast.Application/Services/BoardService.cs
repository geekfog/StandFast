using FluentValidation;
using StandFast.Application.Auditing;
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
            priorTurns);
    }

    public Task<BoardParticipantDto?> AdvanceAsync(Guid standupId, DateOnly meetingDate, Guid personId, CancellationToken cancellationToken = default) =>
        ChangeStateAsync(standupId, meetingDate, personId, AttendanceTransitions.Advance, cancellationToken);

    public Task<BoardParticipantDto?> RevertAsync(Guid standupId, DateOnly meetingDate, Guid personId, CancellationToken cancellationToken = default) =>
        ChangeStateAsync(standupId, meetingDate, personId, AttendanceTransitions.Revert, cancellationToken);

    public async Task<BoardParticipantDto?> SaveUpdateAsync(ParticipantUpdateDto update, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(update, cancellationToken);

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
        StandupMeeting meeting = existing ?? new StandupMeeting { StandupId = standupId, MeetingDate = meetingDate, CreatedUtc = clock.UtcNow };

        meeting.LeaderPersonId = personId;
        meeting.ModifiedUtc = existing is null ? null : clock.UtcNow;

        await standups.UpsertMeetingAsync(meeting, cancellationToken);
        audit.Record(AuditEvents.LeaderChanged, MeetingAuditTargetType, MeetingTargetId(standupId, meetingDate), leader is null ? "Leader cleared." : $"{leader.DisplayName} is leading.");

        return true;
    }

    public Task<IReadOnlyCollection<DateOnly>> GetPresentedDatesAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        entries.GetPresentedDatesAsync(standupId, from, to, cancellationToken);

    private async Task<BoardParticipantDto?> ChangeStateAsync(Guid standupId, DateOnly meetingDate, Guid personId, Func<AttendanceState, AttendanceState> transition, CancellationToken cancellationToken)
    {
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
