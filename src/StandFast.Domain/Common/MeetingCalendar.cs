namespace StandFast.Domain.Common;

/// <summary>Week/date arithmetic shared by the board UI, the week strip, and storage key generation. Keeps "what does this week look like" in one place.</summary>
public static class MeetingCalendar
{
    /// <summary>First day rendered by the week strip. Scrum weeks are conventionally Monday-based.</summary>
    public const DayOfWeek FirstDayOfWeek = DayOfWeek.Monday;

    public const int DaysPerWeek = 7;

    /// <summary>Round-trippable, sortable date form used in storage keys and route parameters.</summary>
    public const string DateKeyFormat = "yyyyMMdd";

    /// <summary>Route/query form of a meeting date (ISO 8601 date).</summary>
    public const string DateRouteFormat = "yyyy-MM-dd";

    public static DateOnly StartOfWeek(DateOnly date)
    {
        int offset = ((int)date.DayOfWeek - (int)FirstDayOfWeek + DaysPerWeek) % DaysPerWeek;
        return date.AddDays(-offset);
    }

    public static IReadOnlyList<DateOnly> Week(DateOnly anyDayInWeek)
    {
        DateOnly start = StartOfWeek(anyDayInWeek);
        return [.. Enumerable.Range(0, DaysPerWeek).Select(start.AddDays)];
    }

    public static string ToDateKey(DateOnly date) => date.ToString(DateKeyFormat, System.Globalization.CultureInfo.InvariantCulture);

    public static DateOnly FromDateKey(string dateKey) => DateOnly.ParseExact(dateKey, DateKeyFormat, System.Globalization.CultureInfo.InvariantCulture);

    public static string ToRouteValue(DateOnly date) => date.ToString(DateRouteFormat, System.Globalization.CultureInfo.InvariantCulture);

    public static DateOnly? ParseRouteValue(string? value) =>
        DateOnly.TryParseExact(value, DateRouteFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateOnly parsed) ? parsed : null;
}
