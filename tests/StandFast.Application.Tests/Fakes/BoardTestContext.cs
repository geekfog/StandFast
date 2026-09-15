using StandFast.Application.Auditing;
using StandFast.Application.Services;
using StandFast.Application.Validation;
using StandFast.Domain.Entities;

namespace StandFast.Application.Tests.Fakes;

/// <summary>Builds a fully wired <see cref="BoardService"/> over in-memory storage, so each test states only the data that matters to it.</summary>
public sealed class BoardTestContext
{
    public static readonly DateOnly Today = new(2026, 9, 15);
    public static readonly DateOnly Yesterday = Today.AddDays(-1);
    public static readonly DateTimeOffset Now = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);

    public BoardTestContext()
    {
        Clock = new FixedClock(Now);
        Service = new BoardService(Standups, People, Entries, Clock, new ParticipantUpdateDtoValidator(), Audit);
    }

    public InMemoryPersonRepository People { get; } = new();

    public InMemoryStandupRepository Standups { get; } = new();

    public InMemoryStandupEntryRepository Entries { get; } = new();

    public FixedClock Clock { get; }

    public IAuditLog Audit { get; } = new RecordingAuditLog();

    public BoardService Service { get; }

    public Standup Standup { get; } = new() { Name = "Platform daily" };

    public async Task<Person> AddMemberAsync(string firstName, string lastName)
    {
        await Standups.UpsertAsync(Standup);

        Person person = new() { FirstName = firstName, LastName = lastName, Email = $"{firstName}.{lastName}@example.com".ToLowerInvariant() };
        await People.UpsertAsync(person);
        await Standups.UpsertMemberAsync(new StandupMember { StandupId = Standup.Id, PersonId = person.Id, DisplayOrder = 10 });

        return person;
    }
}

/// <summary>Captures audit calls so tests can assert an action was recorded without a logging pipeline.</summary>
public sealed class RecordingAuditLog : IAuditLog
{
    public List<(string EventName, string TargetType, string TargetId, string Summary)> Records { get; } = [];

    public void Record(string eventName, string targetType, string targetId, string summary) => Records.Add((eventName, targetType, targetId, summary));
}
