using StandFast.Infrastructure.Storage;

namespace StandFast.Infrastructure.Tests.Storage;

/// <summary>The inverted row key is what makes "current plus prior entry" a single range query, so its ordering is pinned by tests.</summary>
public sealed class StorageKeysTests
{
    private static readonly DateOnly Today = new(2026, 9, 15);

    private static readonly char[] ForbiddenKeyCharacters = ['/', '#', '?', (char)92];

    [Fact]
    public void EntryRowKey_SortsNewestFirst()
    {
        string today = StorageKeys.EntryRowKey(Today);
        string yesterday = StorageKeys.EntryRowKey(Today.AddDays(-1));
        string tomorrow = StorageKeys.EntryRowKey(Today.AddDays(1));

        Assert.True(string.CompareOrdinal(today, yesterday) < 0, "An earlier date must sort after a later one.");
        Assert.True(string.CompareOrdinal(tomorrow, today) < 0, "A later date must sort before an earlier one.");
    }

    [Fact]
    public void EntryRowKey_IsFixedWidthSoOrdinalComparisonMatchesDateOrder()
    {
        Assert.Equal(8, StorageKeys.EntryRowKey(Today).Length);
        Assert.Equal(8, StorageKeys.EntryRowKey(new DateOnly(2001, 1, 1)).Length);
    }

    [Theory]
    [InlineData(2026, 9, 15)]
    [InlineData(2001, 1, 1)]
    [InlineData(2099, 12, 31)]
    public void EntryRowKey_RoundTripsBackToTheMeetingDate(int year, int month, int day)
    {
        DateOnly date = new(year, month, day);

        Assert.Equal(date, StorageKeys.MeetingDateFromRowKey(StorageKeys.EntryRowKey(date)));
    }

    [Fact]
    public void EntryPartitionKey_GroupsOneParticipantsWholeHistoryTogether()
    {
        Guid standupId = Guid.CreateVersion7();
        Guid personId = Guid.CreateVersion7();

        string first = StorageKeys.EntryPartitionKey(standupId, personId);
        string second = StorageKeys.EntryPartitionKey(standupId, personId);

        Assert.Equal(first, second);
        Assert.Contains(StorageKeys.KeySeparator, first, StringComparison.Ordinal);
        // Azure Table keys reject forward slash, backslash, hash and question mark; a composite key must never introduce one.
        Assert.All(ForbiddenKeyCharacters, forbidden => Assert.DoesNotContain(forbidden, first));
    }
}
