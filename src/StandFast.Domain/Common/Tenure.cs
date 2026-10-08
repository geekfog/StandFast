namespace StandFast.Domain.Common;

/// <summary>Time between two dates broken down greedily: whole years first, then whole months of what remains, then whole weeks, then days.</summary>
public readonly record struct Tenure(int Years, int Months, int Weeks, int Days)
{
    private const int MonthsPerYear = 12;

    /// <summary>Tenure from <paramref name="start"/> to <paramref name="asOf"/>, or null when <paramref name="asOf"/> falls before the start.</summary>
    public static Tenure? Between(DateOnly start, DateOnly asOf)
    {
        if (asOf < start)
        {
            return null;
        }

        int totalMonths = ((asOf.Year - start.Year) * MonthsPerYear) + asOf.Month - start.Month;

        // Each candidate is measured from the start date itself, so a start on the 31st clamps to a shorter month without drifting later months.
        if (start.AddMonths(totalMonths) > asOf)
        {
            totalMonths--;
        }

        int remainingDays = asOf.DayNumber - start.AddMonths(totalMonths).DayNumber;

        return new Tenure(totalMonths / MonthsPerYear, totalMonths % MonthsPerYear, remainingDays / MeetingCalendar.DaysPerWeek, remainingDays % MeetingCalendar.DaysPerWeek);
    }
}
