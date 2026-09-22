using StandFast.Domain.Abstractions;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Fakes;

/// <summary>In-memory stand-ins for the storage layer. Application logic is tested against the repository interfaces, with no Azure dependency.</summary>
public sealed class InMemoryPersonRepository : IPersonRepository
{
    private readonly Dictionary<Guid, Person> people = [];

    public Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Person>>([.. people.Values]);

    public Task<Person?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(people.GetValueOrDefault(id));

    public Task UpsertAsync(Person person, CancellationToken cancellationToken = default)
    {
        people[person.Id] = person;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        people.Remove(id);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryStandupRepository : IStandupRepository
{
    private readonly Dictionary<Guid, Standup> standups = [];
    private readonly Dictionary<(Guid StandupId, Guid PersonId), StandupMember> members = [];

    public Task<IReadOnlyList<Standup>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Standup>>([.. standups.Values]);

    public Task<Standup?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(standups.GetValueOrDefault(id));

    public Task UpsertAsync(Standup standup, CancellationToken cancellationToken = default)
    {
        standups[standup.Id] = standup;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        standups.Remove(id);
        foreach ((Guid StandupId, Guid PersonId) key in members.Keys.Where(key => key.StandupId == id).ToList())
        {
            members.Remove(key);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StandupMember>> GetMembersAsync(Guid standupId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StandupMember>>([.. members.Values.Where(member => member.StandupId == standupId)]);

    public Task<IReadOnlyList<StandupMember>> GetAllMembersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StandupMember>>([.. members.Values]);

    public Task UpsertMemberAsync(StandupMember member, CancellationToken cancellationToken = default)
    {
        members[(member.StandupId, member.PersonId)] = member;
        return Task.CompletedTask;
    }

    public Task RemoveMemberAsync(Guid standupId, Guid personId, CancellationToken cancellationToken = default)
    {
        members.Remove((standupId, personId));
        return Task.CompletedTask;
    }
}

/// <summary>Mirrors the real repository's contract: the prior entry is the most recent one strictly before the requested date.</summary>
public sealed class InMemoryStandupEntryRepository : IStandupEntryRepository
{
    private readonly Dictionary<(Guid StandupId, Guid PersonId, DateOnly MeetingDate), StandupEntry> entries = [];

    public Task<StandupEntryPair> GetCurrentAndPriorAsync(Guid standupId, Guid personId, DateOnly meetingDate, CancellationToken cancellationToken = default)
    {
        StandupEntry? current = entries.GetValueOrDefault((standupId, personId, meetingDate));
        StandupEntry? prior = entries.Values
            .Where(entry => entry.StandupId == standupId && entry.PersonId == personId && entry.MeetingDate < meetingDate)
            .MaxBy(entry => entry.MeetingDate);

        return Task.FromResult(new StandupEntryPair(current, prior));
    }

    public Task UpsertAsync(StandupEntry entry, CancellationToken cancellationToken = default)
    {
        entries[(entry.StandupId, entry.PersonId, entry.MeetingDate)] = entry;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<DateOnly>> GetPresentedDatesAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<DateOnly> dates = entries.Values
            .Where(entry => entry.StandupId == standupId && entry.State == AttendanceState.Presented && entry.MeetingDate >= from && entry.MeetingDate <= to)
            .Select(entry => entry.MeetingDate)
            .ToHashSet();

        return Task.FromResult(dates);
    }

    public Task<IReadOnlyList<Presentation>> GetPresentationsAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Presentation> presentations =
        [
            .. entries.Values
                .Where(entry => entry.StandupId == standupId && entry.MeetingDate >= from && entry.MeetingDate <= to)
                .Select(entry => entry.CompletedTurn)
                .OfType<Presentation>(),
        ];

        return Task.FromResult(presentations);
    }
}

public sealed class InMemoryUserPreferencesRepository : IUserPreferencesRepository
{
    private readonly Dictionary<string, UserPreferences> preferences = [];

    public Task<UserPreferences?> GetAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(preferences.GetValueOrDefault(userId));

    public Task UpsertAsync(UserPreferences settings, CancellationToken cancellationToken = default)
    {
        preferences[settings.UserId] = settings;
        return Task.CompletedTask;
    }
}
