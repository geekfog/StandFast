using StandFast.Domain.Common;

namespace StandFast.Ui.Common;

/// <summary>Display formats used across the board and admin screens, so a date or timestamp reads the same everywhere.</summary>
public static class UiFormats
{
    public const string WeekdayAbbreviation = "ddd";

    /// <summary>Weekday in full, for a table column that names the day beside its date.</summary>
    public const string WeekdayName = "dddd";
    public const string LongDate = "dddd, d MMMM yyyy";
    public const string ShortDate = "d MMM yyyy";

    /// <summary>Date without its year, for axis ticks and table headings where the year is already given by the range.</summary>
    public const string DayAndMonth = "d MMM";
    public const string TimeOfDay = "t";
    public const string DateAndTime = "d MMM yyyy HH:mm";

    /// <summary>Used where the exact moment matters, such as the presented time that orders the Presented column.</summary>
    public const string DateAndTimeWithSeconds = "d MMM yyyy HH:mm:ss";

    /// <summary>Shown in place of a turn number for someone who was not at the previous standup, so they sort last in the reader's head.</summary>
    public const string NoPriorTurnSymbol = PresentationOrder.NoTurnSymbol;

    /// <summary>Stands in for a figure that cannot be worked out yet, so a table cell reads as unanswerable rather than as zero.</summary>
    public const string UnavailableSymbol = "—";

    /// <summary>Ordinal form of a turn number, for example 1st or 23rd. Used in the board's prior-turn tooltips.</summary>
    public static string ToOrdinal(this int value)
    {
        int lastTwoDigits = value % 100;
        string suffix = lastTwoDigits is >= 11 and <= 13
            ? "th"
            : (value % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };

        return string.Concat(value.ToString(), suffix);
    }

    /// <summary>Renders a UTC timestamp in the app's display time zone, for "saved at" style labels.</summary>
    public static string ToLocalDisplay(this DateTimeOffset? timestamp, TimeZoneInfo timeZone, string format = DateAndTime) =>
        timestamp is null ? string.Empty : TimeZoneInfo.ConvertTime(timestamp.Value, timeZone).ToString(format);
}
