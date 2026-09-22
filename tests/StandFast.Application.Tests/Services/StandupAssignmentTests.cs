using StandFast.Application.Dtos;
using StandFast.Application.Services;
using StandFast.Application.Tests.Fakes;
using StandFast.Application.Validation;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Services;

public sealed class StandupAssignmentTests
{
    private readonly InMemoryStandupRepository standups = new();
    private readonly InMemoryPersonRepository people = new();
    private readonly StandupService service;

    public StandupAssignmentTests() =>
        service = new StandupService(standups, people, new FixedClock(BoardTestContext.Now), new StandupEditDtoValidator(), new RecordingAuditLog());

    [Fact]
    public async Task GetStandupNamesByPersonAsync_ListsEveryStandupAPersonPresentsAtAlphabetically()
    {
        Standup platform = await AddStandupAsync("Platform daily");
        Standup web = await AddStandupAsync("Aardvark web");
        Guid personId = Guid.CreateVersion7();

        await AddMemberAsync(platform, personId, RosterRole.Presenter);
        await AddMemberAsync(web, personId, RosterRole.Presenter);

        StandupNamesByPersonDto assignments = await service.GetStandupNamesByPersonAsync();

        Assert.Equal(["Aardvark web", "Platform daily"], assignments.For(RosterRole.Presenter, personId));
    }

    [Fact]
    public async Task GetStandupNamesByPersonAsync_KeepsEachRoleSeparate()
    {
        Standup platform = await AddStandupAsync("Platform daily");
        Standup web = await AddStandupAsync("Aardvark web");
        Guid personId = Guid.CreateVersion7();

        await AddMemberAsync(platform, personId, RosterRole.Presenter);
        await AddMemberAsync(web, personId, RosterRole.Leader);

        StandupNamesByPersonDto assignments = await service.GetStandupNamesByPersonAsync();

        Assert.Equal(["Platform daily"], assignments.For(RosterRole.Presenter, personId));
        Assert.Equal(["Aardvark web"], assignments.For(RosterRole.Leader, personId));
    }

    [Fact]
    public async Task GetStandupNamesByPersonAsync_ReturnsNothingForAnyoneWithNoStandups()
    {
        Standup platform = await AddStandupAsync("Platform daily");
        Guid member = Guid.CreateVersion7();
        Guid nonMember = Guid.CreateVersion7();

        await AddMemberAsync(platform, member, RosterRole.Presenter);

        StandupNamesByPersonDto assignments = await service.GetStandupNamesByPersonAsync();

        Assert.NotEmpty(assignments.For(RosterRole.Presenter, member));
        Assert.Empty(assignments.For(RosterRole.Presenter, nonMember));
    }

    [Fact]
    public async Task GetStandupNamesByPersonAsync_IgnoresInactiveMemberships()
    {
        Standup platform = await AddStandupAsync("Platform daily");
        Guid personId = Guid.CreateVersion7();

        await standups.UpsertMemberAsync(new StandupMember { StandupId = platform.Id, PersonId = personId, IsActive = false });

        StandupNamesByPersonDto assignments = await service.GetStandupNamesByPersonAsync();

        Assert.Empty(assignments.For(RosterRole.Presenter, personId));
    }

    [Fact]
    public async Task GetStandupNamesByPersonAsync_DropsMembershipsWhoseStandupIsGone()
    {
        Standup platform = await AddStandupAsync("Platform daily");
        Guid personId = Guid.CreateVersion7();
        await AddMemberAsync(platform, personId, RosterRole.Presenter);

        // Leaves the membership row behind, which is what an interrupted delete would do.
        await standups.UpsertAsync(platform);
        await standups.DeleteAsync(platform.Id);
        await standups.UpsertMemberAsync(new StandupMember { StandupId = platform.Id, PersonId = personId });

        StandupNamesByPersonDto assignments = await service.GetStandupNamesByPersonAsync();

        Assert.Empty(assignments.For(RosterRole.Presenter, personId));
    }

    [Fact]
    public async Task AddMemberAsync_KeepsTheTwoRostersIndependent()
    {
        Standup platform = await AddStandupAsync("Platform daily");
        Person person = new() { FirstName = "Ada", LastName = "Lovelace", Email = "ada.lovelace@example.com" };
        await people.UpsertAsync(person);

        await service.AddMemberAsync(platform.Id, person.Id, RosterRole.Presenter);
        await service.AddMemberAsync(platform.Id, person.Id, RosterRole.Leader);
        await service.RemoveMemberAsync(platform.Id, person.Id, RosterRole.Presenter);

        Assert.Empty(await service.GetMembersAsync(platform.Id, RosterRole.Presenter));
        Assert.Single(await service.GetMembersAsync(platform.Id, RosterRole.Leader));
    }

    private async Task<Standup> AddStandupAsync(string name)
    {
        Standup standup = new() { Name = name, CreatedUtc = BoardTestContext.Now };
        await standups.UpsertAsync(standup);
        return standup;
    }

    private Task AddMemberAsync(Standup standup, Guid personId, RosterRole role) =>
        standups.UpsertMemberAsync(new StandupMember { StandupId = standup.Id, PersonId = personId, Role = role, DisplayOrder = 10 });
}
