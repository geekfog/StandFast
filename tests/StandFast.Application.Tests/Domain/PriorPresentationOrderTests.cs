using StandFast.Domain.Common;
using StandFast.Domain.Entities;

namespace StandFast.Application.Tests.Domain;

public sealed class PriorPresentationOrderTests
{
    private static readonly DateOnly Yesterday = new(2026, 9, 14);
    private static readonly DateTimeOffset NineAm = new(2026, 9, 14, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Rank_NumbersParticipantsByWhenTheyPresented()
    {
        Guid first = Guid.CreateVersion7();
        Guid second = Guid.CreateVersion7();
        Guid third = Guid.CreateVersion7();

        IReadOnlyDictionary<Guid, int> order = PriorPresentationOrder.Rank(
        [
            Presented(second, Yesterday, NineAm.AddMinutes(2)),
            Presented(third, Yesterday, NineAm.AddMinutes(5)),
            Presented(first, Yesterday, NineAm),
        ]);

        Assert.Equal(1, order[first]);
        Assert.Equal(2, order[second]);
        Assert.Equal(3, order[third]);
    }

    [Fact]
    public void Rank_ExcludesAnyoneWhoDidNotPresent()
    {
        Guid presenter = Guid.CreateVersion7();
        Guid absentee = Guid.CreateVersion7();

        IReadOnlyDictionary<Guid, int> order = PriorPresentationOrder.Rank(
        [
            Presented(presenter, Yesterday, NineAm),
            new StandupEntry { PersonId = absentee, MeetingDate = Yesterday, Update = "Sent notes but did not attend." },
            null,
        ]);

        Assert.Equal(1, order[presenter]);
        Assert.False(order.ContainsKey(absentee));
    }

    [Fact]
    public void Rank_UsesOnlyTheMostRecentMeeting()
    {
        Guid recentSpeaker = Guid.CreateVersion7();
        Guid staleSpeaker = Guid.CreateVersion7();

        // The stale entry is older but has an earlier time of day, so ranking by timestamp alone would wrongly put it first.
        IReadOnlyDictionary<Guid, int> order = PriorPresentationOrder.Rank(
        [
            Presented(staleSpeaker, Yesterday.AddDays(-7), NineAm.AddDays(-7)),
            Presented(recentSpeaker, Yesterday, NineAm.AddMinutes(30)),
        ]);

        Assert.Equal(1, order[recentSpeaker]);
        Assert.False(order.ContainsKey(staleSpeaker));
    }

    [Fact]
    public void Rank_ReturnsNothingWhenNobodyHasPresentedBefore()
    {
        Assert.Empty(PriorPresentationOrder.Rank([null, null]));
    }

    private static StandupEntry Presented(Guid personId, DateOnly meetingDate, DateTimeOffset presentedUtc) =>
        new() { PersonId = personId, MeetingDate = meetingDate, PresentedUtc = presentedUtc };
}
