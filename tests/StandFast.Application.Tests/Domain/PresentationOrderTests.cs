using StandFast.Domain.Common;

namespace StandFast.Application.Tests.Domain;

public sealed class PresentationOrderTests
{
    private static readonly DateOnly Yesterday = new(2026, 9, 14);
    private static readonly DateTimeOffset NineAm = new(2026, 9, 14, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WithinMeeting_NumbersParticipantsByWhenTheyPresented()
    {
        Guid first = Guid.CreateVersion7();
        Guid second = Guid.CreateVersion7();
        Guid third = Guid.CreateVersion7();

        IReadOnlyDictionary<Guid, int> order = PresentationOrder.WithinMeeting(
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
    public void AtMostRecentMeeting_NumbersParticipantsByWhenTheyPresented()
    {
        Guid first = Guid.CreateVersion7();
        Guid second = Guid.CreateVersion7();

        IReadOnlyDictionary<Guid, int> order = PresentationOrder.AtMostRecentMeeting(
        [
            Presented(second, Yesterday, NineAm.AddMinutes(2)),
            Presented(first, Yesterday, NineAm),
        ]);

        Assert.Equal(1, order[first]);
        Assert.Equal(2, order[second]);
    }

    [Fact]
    public void AtMostRecentMeeting_UsesOnlyTheLatestMeeting()
    {
        Guid recentSpeaker = Guid.CreateVersion7();
        Guid staleSpeaker = Guid.CreateVersion7();

        // The stale turn is older but has an earlier time of day, so ranking by timestamp alone would wrongly put it first.
        IReadOnlyDictionary<Guid, int> order = PresentationOrder.AtMostRecentMeeting(
        [
            Presented(staleSpeaker, Yesterday.AddDays(-7), NineAm.AddDays(-7)),
            Presented(recentSpeaker, Yesterday, NineAm.AddMinutes(30)),
        ]);

        Assert.Equal(1, order[recentSpeaker]);
        Assert.False(order.ContainsKey(staleSpeaker));
    }

    [Fact]
    public void AtMostRecentMeeting_ReturnsNothingWhenNobodyHasPresentedBefore()
    {
        Assert.Empty(PresentationOrder.AtMostRecentMeeting([]));
    }

    private static Presentation Presented(Guid personId, DateOnly meetingDate, DateTimeOffset presentedUtc) => new(personId, meetingDate, presentedUtc);
}
