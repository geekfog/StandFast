using StandFast.Application.Services;
using StandFast.Application.Tests.Fakes;
using StandFast.Application.Validation;
using StandFast.Domain.Entities;

namespace StandFast.Application.Tests.Services;

public sealed class StandupAssignmentTests
{
    private readonly InMemoryStandupRepository standups = new();
    private readonly InMemoryPersonRepository people = new();
    private readonly StandupService service;

    public StandupAssignmentTests() =>
        service = new StandupService(standups, people, new FixedClock(BoardTestContext.Now), new StandupEditDtoValidator(), new RecordingAuditLog());

    [Fact]
    public async Task GetStandupNamesByPersonAsync_ListsEveryStandupAPersonIsOnAlphabetically()
    {
        Standup platform = await AddStandupAsync("Platform daily");
        Standup web = await AddStandupAsync("Aardvark web");
        Guid personId = Guid.CreateVersion7();

        await AddMemberAsync(platform, personId);
        await AddMemberAsync(web, personId);

        IReadOnlyDictionary<Guid, IReadOnlyList<string>> assignments = await service.GetStandupNamesByPersonAsync();

        Assert.Equal(["Aardvark web", "Platform daily"], assignments[personId]);
    }

    [Fact]
    public async Task GetStandupNamesByPersonAsync_OmitsAnyoneWithNoStandups()
    {
        Standup platform = await AddStandupAsync("Platform daily");
        Guid member = Guid.CreateVersion7();
        Guid nonMember = Guid.CreateVersion7();

        await AddMemberAsync(platform, member);

        IReadOnlyDictionary<Guid, IReadOnlyList<string>> assignments = await service.GetStandupNamesByPersonAsync();

        Assert.True(assignments.ContainsKey(member));
        Assert.False(assignments.ContainsKey(nonMember));
    }

    [Fact]
    public async Task GetStandupNamesByPersonAsync_IgnoresInactiveMemberships()
    {
        Standup platform = await AddStandupAsync("Platform daily");
        Guid personId = Guid.CreateVersion7();

        await standups.UpsertMemberAsync(new StandupMember { StandupId = platform.Id, PersonId = personId, IsActive = false });

        IReadOnlyDictionary<Guid, IReadOnlyList<string>> assignments = await service.GetStandupNamesByPersonAsync();

        Assert.False(assignments.ContainsKey(personId));
    }

    [Fact]
    public async Task GetStandupNamesByPersonAsync_DropsMembershipsWhoseStandupIsGone()
    {
        Standup platform = await AddStandupAsync("Platform daily");
        Guid personId = Guid.CreateVersion7();
        await AddMemberAsync(platform, personId);

        // Leaves the membership row behind, which is what an interrupted delete would do.
        await standups.UpsertAsync(platform);
        await standups.DeleteAsync(platform.Id);
        await standups.UpsertMemberAsync(new StandupMember { StandupId = platform.Id, PersonId = personId });

        IReadOnlyDictionary<Guid, IReadOnlyList<string>> assignments = await service.GetStandupNamesByPersonAsync();

        Assert.False(assignments.ContainsKey(personId));
    }

    private async Task<Standup> AddStandupAsync(string name)
    {
        Standup standup = new() { Name = name, CreatedUtc = BoardTestContext.Now };
        await standups.UpsertAsync(standup);
        return standup;
    }

    private Task AddMemberAsync(Standup standup, Guid personId) =>
        standups.UpsertMemberAsync(new StandupMember { StandupId = standup.Id, PersonId = personId, DisplayOrder = 10 });
}
