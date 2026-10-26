using StandFast.Application.Dtos;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Mapping;

public sealed class StandupNamesByPersonDtoTests
{
    [Fact]
    public void RolesFor_ListsEachRoleHeldOnAnyStandup_InDisplayOrder_AndNoneForSomeoneOnNoRoster()
    {
        Guid both = Guid.CreateVersion7();
        Guid leaderOnly = Guid.CreateVersion7();
        StandupNamesByPersonDto names = new(new Dictionary<RosterRole, IReadOnlyDictionary<Guid, IReadOnlyList<string>>>
        {
            [RosterRole.Leader] = new Dictionary<Guid, IReadOnlyList<string>> { [both] = ["Platform daily"], [leaderOnly] = ["Platform daily"] },
            [RosterRole.Presenter] = new Dictionary<Guid, IReadOnlyList<string>> { [both] = ["Platform daily"] },
        });

        Assert.Equal([RosterRole.Presenter, RosterRole.Leader], names.RolesFor(both));
        Assert.Equal([RosterRole.Leader], names.RolesFor(leaderOnly));
        Assert.Empty(names.RolesFor(Guid.CreateVersion7()));
    }
}
