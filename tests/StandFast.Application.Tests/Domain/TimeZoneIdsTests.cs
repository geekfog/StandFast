using StandFast.Domain.Common;

namespace StandFast.Application.Tests.Domain;

public sealed class TimeZoneIdsTests
{
    private static readonly TimeZoneInfo Chicago = TimeZoneIds.Resolve("America/Chicago")!;
    private static readonly TimeZoneInfo NewYork = TimeZoneIds.Resolve("America/New_York")!;
    private static readonly TimeZoneInfo Phoenix = TimeZoneIds.Resolve("America/Phoenix")!;
    private static readonly TimeZoneInfo Kolkata = TimeZoneIds.Resolve("Asia/Kolkata")!;

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("Mars/Olympus_Mons")]
    public void Resolve_ReturnsNullForABlankOrUnknownId(string? id)
    {
        Assert.Null(TimeZoneIds.Resolve(id));
    }

    [Fact]
    public void ToInstant_ReadsTheTimeInTheGivenZone()
    {
        DateTimeOffset instant = TimeZoneIds.ToInstant(new DateOnly(2026, 9, 29), new TimeOnly(9, 0), Chicago);

        Assert.Equal(new DateTimeOffset(2026, 9, 29, 14, 0, 0, TimeSpan.Zero), instant.ToUniversalTime());
    }

    [Fact]
    public void ToInstant_MovesATimeInADaylightSavingGapToTheEndOfTheGap()
    {
        DateTimeOffset instant = TimeZoneIds.ToInstant(new DateOnly(2026, 3, 8), new TimeOnly(2, 30), Chicago);

        Assert.Equal(new DateTimeOffset(2026, 3, 8, 8, 0, 0, TimeSpan.Zero), instant.ToUniversalTime());
    }

    [Theory]
    [InlineData(2026, 9, 29, 1)]
    [InlineData(2026, 1, 15, 1)]
    public void OffsetFrom_IsPositiveForAZoneAhead(int year, int month, int day, int expectedHours)
    {
        DateTimeOffset instant = TimeZoneIds.ToInstant(new DateOnly(year, month, day), new TimeOnly(9, 0), Chicago);

        Assert.Equal(TimeSpan.FromHours(expectedHours), TimeZoneIds.OffsetFrom(NewYork, Chicago, instant));
    }

    [Theory]
    [InlineData(7, -2)]
    [InlineData(1, -1)]
    public void OffsetFrom_FollowsDaylightSavingOnTheDate(int month, int expectedHours)
    {
        DateTimeOffset instant = TimeZoneIds.ToInstant(new DateOnly(2026, month, 15), new TimeOnly(9, 0), Chicago);

        Assert.Equal(TimeSpan.FromHours(expectedHours), TimeZoneIds.OffsetFrom(Phoenix, Chicago, instant));
    }

    [Fact]
    public void OffsetFrom_KeepsAHalfHourZone()
    {
        DateTimeOffset instant = TimeZoneIds.ToInstant(new DateOnly(2026, 9, 29), new TimeOnly(9, 0), Chicago);

        Assert.Equal(TimeSpan.FromHours(10.5), TimeZoneIds.OffsetFrom(Kolkata, Chicago, instant));
    }
}
