using StandFast.Application.Dtos;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Mapping;

public sealed class StandupNamesByPersonDtoTests
{
    private static readonly StandupRefDto Platform = new(Guid.CreateVersion7(), "Platform daily");
    private static readonly StandupRefDto Web = new(Guid.CreateVersion7(), "Web daily");

    [Fact]
    public void RolesFor_ListsEachRoleHeldOnAnyStandup_InDisplayOrder_AndNoneForSomeoneOnNoRoster()
    {
        Guid both = Guid.CreateVersion7();
        Guid leaderOnly = Guid.CreateVersion7();
        Guid guestOnly = Guid.CreateVersion7();
        StandupNamesByPersonDto names = new(new Dictionary<RosterRole, IReadOnlyDictionary<Guid, IReadOnlyList<StandupRefDto>>>
        {
            [RosterRole.Leader] = new Dictionary<Guid, IReadOnlyList<StandupRefDto>> { [both] = [Platform], [leaderOnly] = [Platform] },
            [RosterRole.Presenter] = new Dictionary<Guid, IReadOnlyList<StandupRefDto>> { [both] = [Platform] },
            [RosterRole.Guest] = new Dictionary<Guid, IReadOnlyList<StandupRefDto>> { [guestOnly] = [Platform] },
        });

        Assert.Equal([RosterRole.Presenter, RosterRole.Leader], names.RolesFor(both));
        Assert.Equal([RosterRole.Leader], names.RolesFor(leaderOnly));
        Assert.Equal([RosterRole.Guest], names.RolesFor(guestOnly));
        Assert.Empty(names.RolesFor(Guid.CreateVersion7()));
    }

    [Fact]
    public void RolesFor_WithAStandup_ListsOnlyTheRolesHeldOnThatStandup()
    {
        Guid personId = Guid.CreateVersion7();
        StandupNamesByPersonDto names = new(new Dictionary<RosterRole, IReadOnlyDictionary<Guid, IReadOnlyList<StandupRefDto>>>
        {
            [RosterRole.Presenter] = new Dictionary<Guid, IReadOnlyList<StandupRefDto>> { [personId] = [Platform] },
            [RosterRole.Leader] = new Dictionary<Guid, IReadOnlyList<StandupRefDto>> { [personId] = [Platform, Web] },
            [RosterRole.Guest] = new Dictionary<Guid, IReadOnlyList<StandupRefDto>> { [personId] = [Web] },
        });

        Assert.Equal([RosterRole.Presenter, RosterRole.Leader], names.RolesFor(personId, Platform.Id));
        Assert.Equal([RosterRole.Leader, RosterRole.Guest], names.RolesFor(personId, Web.Id));
        Assert.Empty(names.RolesFor(personId, Guid.CreateVersion7()));
    }
}
