using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Infrastructure.Tests.Integration;

/// <summary>
/// Exercises the inverted row key against real table storage. The unit tests prove the key sorts correctly in isolation; these prove the
/// service actually returns the rows in that order, which is the assumption the whole board rests on.
/// </summary>
public sealed class StandupEntryRepositoryTests : IClassFixture<AzuriteTableFixture>
{
    private static readonly DateOnly Today = new(2026, 9, 15);

    private readonly AzuriteTableFixture fixture;
    private readonly Guid standupId = Guid.CreateVersion7();
    private readonly Guid personId = Guid.CreateVersion7();

    public StandupEntryRepositoryTests(AzuriteTableFixture fixture) => this.fixture = fixture;

    [AzuriteFact]
    public async Task GetCurrentAndPriorAsync_ReturnsNothingForAParticipantWithNoHistory()
    {
        StandupEntryPair pair = await fixture.Entries.GetCurrentAndPriorAsync(standupId, Guid.CreateVersion7(), Today);

        Assert.Null(pair.Current);
        Assert.Null(pair.Prior);
    }

    [AzuriteFact]
    public async Task GetCurrentAndPriorAsync_ReturnsTheRequestedDateAndTheMostRecentEarlierDate()
    {
        await SeedAsync(Today.AddDays(-14), "Two weeks ago");
        await SeedAsync(Today.AddDays(-1), "Yesterday");
        await SeedAsync(Today, "Today");

        StandupEntryPair pair = await fixture.Entries.GetCurrentAndPriorAsync(standupId, personId, Today);

        Assert.Equal("Today", pair.Current?.Update);
        Assert.Equal("Yesterday", pair.Prior?.Update);
        Assert.Equal(Today.AddDays(-1), pair.Prior?.MeetingDate);
    }

    [AzuriteFact]
    public async Task GetCurrentAndPriorAsync_SkipsAheadToThePriorEntryWhenTodayHasNothingRecorded()
    {
        await SeedAsync(Today.AddDays(-3), "Three days ago");

        StandupEntryPair pair = await fixture.Entries.GetCurrentAndPriorAsync(standupId, personId, Today);

        Assert.Null(pair.Current);
        Assert.Equal("Three days ago", pair.Prior?.Update);
    }

    [AzuriteFact]
    public async Task GetCurrentAndPriorAsync_IgnoresEntriesAfterTheRequestedDate()
    {
        await SeedAsync(Today.AddDays(-1), "Yesterday");
        await SeedAsync(Today.AddDays(1), "Tomorrow");

        StandupEntryPair pair = await fixture.Entries.GetCurrentAndPriorAsync(standupId, personId, Today);

        Assert.Null(pair.Current);
        Assert.Equal("Yesterday", pair.Prior?.Update);
    }

    [AzuriteFact]
    public async Task UpsertAsync_ReplacesTheEntryForTheSameDateRatherThanAddingASecondRow()
    {
        await SeedAsync(Today, "First attempt");
        await SeedAsync(Today, "Corrected", AttendanceState.Presented);

        StandupEntryPair pair = await fixture.Entries.GetCurrentAndPriorAsync(standupId, personId, Today);

        Assert.Equal("Corrected", pair.Current?.Update);
        Assert.Equal(AttendanceState.Presented, pair.Current?.State);
        Assert.Null(pair.Prior);
    }

    [AzuriteFact]
    public async Task GetCurrentAndPriorAsync_KeepsParticipantsInSeparatePartitions()
    {
        Guid otherPersonId = Guid.CreateVersion7();
        await SeedAsync(Today, "Mine");
        await fixture.Entries.UpsertAsync(new StandupEntry { StandupId = standupId, PersonId = otherPersonId, MeetingDate = Today, Update = "Theirs" });

        StandupEntryPair mine = await fixture.Entries.GetCurrentAndPriorAsync(standupId, personId, Today);
        StandupEntryPair theirs = await fixture.Entries.GetCurrentAndPriorAsync(standupId, otherPersonId, Today);

        Assert.Equal("Mine", mine.Current?.Update);
        Assert.Equal("Theirs", theirs.Current?.Update);
    }

    [AzuriteFact]
    public async Task GetPresentedDatesAsync_ReturnsOnlyPresentedDatesInsideTheRange()
    {
        await SeedAsync(Today.AddDays(-10), "Before the range", AttendanceState.Presented);
        await SeedAsync(Today.AddDays(-2), "Presented", AttendanceState.Presented);
        await SeedAsync(Today.AddDays(-1), "Only marked available");
        await SeedAsync(Today.AddDays(3), "After the range", AttendanceState.Presented);

        IReadOnlyCollection<DateOnly> dates = await fixture.Entries.GetPresentedDatesAsync(standupId, Today.AddDays(-6), Today);

        Assert.Equal([Today.AddDays(-2)], dates);
    }

    [AzuriteFact]
    public async Task GetPresentedDatesAsync_CoversEveryParticipantAndReportsEachDateOnce()
    {
        Guid otherPersonId = Guid.CreateVersion7();
        await SeedAsync(Today, "Mine", AttendanceState.Presented);
        await fixture.Entries.UpsertAsync(new StandupEntry
        {
            StandupId = standupId,
            PersonId = otherPersonId,
            MeetingDate = Today,
            State = AttendanceState.Presented,
        });

        IReadOnlyCollection<DateOnly> dates = await fixture.Entries.GetPresentedDatesAsync(standupId, Today.AddDays(-6), Today);

        Assert.Equal([Today], dates);
    }

    [AzuriteFact]
    public async Task GetPresentedDatesAsync_IgnoresOtherStandups()
    {
        await fixture.Entries.UpsertAsync(new StandupEntry
        {
            StandupId = Guid.CreateVersion7(),
            PersonId = personId,
            MeetingDate = Today,
            State = AttendanceState.Presented,
        });

        IReadOnlyCollection<DateOnly> dates = await fixture.Entries.GetPresentedDatesAsync(standupId, Today.AddDays(-6), Today);

        Assert.Empty(dates);
    }

    private Task SeedAsync(DateOnly meetingDate, string update, AttendanceState state = AttendanceState.Available) =>
        fixture.Entries.UpsertAsync(new StandupEntry
        {
            StandupId = standupId,
            PersonId = personId,
            MeetingDate = meetingDate,
            State = state,
            Update = update,
            UpdateSavedUtc = DateTimeOffset.UtcNow,
        });
}
