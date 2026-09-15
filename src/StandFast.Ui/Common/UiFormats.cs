namespace StandFast.Ui.Common;

/// <summary>Display formats used across the board and admin screens, so a date or timestamp reads the same everywhere.</summary>
public static class UiFormats
{
    public const string WeekdayAbbreviation = "ddd";
    public const string LongDate = "dddd, d MMMM yyyy";
    public const string ShortDate = "d MMM yyyy";
    public const string TimeOfDay = "t";
    public const string DateAndTime = "d MMM yyyy HH:mm";

    /// <summary>Renders a UTC timestamp in the app's display time zone, for "saved at" style labels.</summary>
    public static string ToLocalDisplay(this DateTimeOffset? timestamp, TimeZoneInfo timeZone) =>
        timestamp is null ? string.Empty : TimeZoneInfo.ConvertTime(timestamp.Value, timeZone).ToString(DateAndTime);
}
