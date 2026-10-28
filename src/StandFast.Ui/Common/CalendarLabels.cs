using MudBlazor;
using StandFast.Application.Dtos;
using StandFast.Domain.Enums;

namespace StandFast.Ui.Common;

/// <summary>Wording and icons for holidays, sprints, and days off, so the screens that manage them and the board that shows them agree.</summary>
public static class CalendarLabels
{
    public const string Organization = "Organization";
    public const string Holidays = "Holidays";
    public const string Sprints = "Sprints";
    public const string DaysOff = "Days off";

    public const string OrganizationIcon = Icons.Material.Filled.CorporateFare;
    public const string HolidayIcon = Icons.Material.Filled.Celebration;
    public const string SprintIcon = Icons.Material.Filled.DirectionsRun;
    public const string DaysOffIcon = Icons.Material.Filled.EventBusy;
    public const string SprintNoteIcon = Icons.Material.Filled.StickyNote2;

    public static string Duration(HolidayDuration duration) => duration == HolidayDuration.HalfDay ? "Half day" : "Full day";

    /// <summary>A date range as one label, or a single date when it starts and ends on the same day.</summary>
    public static string Range(DateOnly start, DateOnly end) =>
        start == end ? start.ToString(UiFormats.ShortDate) : $"{start.ToString(UiFormats.ShortDate)} – {end.ToString(UiFormats.ShortDate)}";

    /// <summary>The board header's sprint label: the sprint's name with the working days left in it.</summary>
    public static string SprintCaption(SprintStatusDto sprint) => $"{sprint.Name} ({sprint.DaysLeft.ToDayCount()} left)";

    public static string SprintDescription(SprintStatusDto sprint) =>
        $"{sprint.DaysLeft.ToDayCount()} left in {sprint.Name} ({Range(sprint.StartDate, sprint.EndDate)}), counting this date, weekdays only, less holidays.";

    /// <summary>The days off marker's hover text: whether the person is off on the board's date, and what their days off leave them in the rest of the sprint.</summary>
    public static string AvailabilityDescription(PersonAvailabilityDto availability, SprintStatusDto? sprint)
    {
        string? offOnDate = availability.IsOffOnDate ? $"Off on this date{(availability.OffNote is null ? string.Empty : $": {availability.OffNote}")}." : null;
        string? inSprint = sprint is not null && availability is { DaysOffInSprint: > 0, DaysLeftInSprint: { } daysLeft }
            ? $"{availability.DaysOffInSprint.Value.ToDayCount()} off in the rest of {sprint.Name}, leaving {daysLeft.ToDayCount()} of {sprint.DaysLeft.ToDayCount()} to contribute."
            : null;

        return string.Join(' ', new[] { offOnDate, inSprint }.OfType<string>());
    }
}
