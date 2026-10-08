using StandFast.Domain.Common;

namespace StandFast.Application.Tests.Domain;

public sealed class TenureTests
{
    [Theory]
    [InlineData("2026-10-08", "2026-10-08", 0, 0, 0, 0)] // Start day itself.
    [InlineData("2026-09-28", "2026-10-08", 0, 0, 1, 3)] // Ten days is one week and three days.
    [InlineData("2021-03-01", "2026-10-08", 5, 7, 1, 0)] // Years, then months, then weeks of what remains.
    [InlineData("2025-10-09", "2026-10-08", 0, 11, 4, 1)] // One day short of a year stays in months.
    [InlineData("2026-01-31", "2026-02-28", 0, 1, 0, 0)] // A start on the 31st reaches a month at the end of a shorter month.
    [InlineData("2026-01-31", "2026-03-31", 0, 2, 0, 0)] // Months are counted from the start date, so February's clamp does not drift March.
    [InlineData("2024-02-29", "2025-02-28", 1, 0, 0, 0)] // A leap-day start reaches a year on 28 February.
    public void Between_BreaksDownIntoWholeYearsThenMonthsThenWeeksThenDays(string start, string asOf, int years, int months, int weeks, int days)
    {
        Tenure? tenure = Tenure.Between(DateOnly.Parse(start), DateOnly.Parse(asOf));

        Assert.Equal(new Tenure(years, months, weeks, days), tenure);
    }

    [Fact]
    public void Between_IsNullBeforeTheStartDate()
    {
        Assert.Null(Tenure.Between(new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 8)));
    }
}
