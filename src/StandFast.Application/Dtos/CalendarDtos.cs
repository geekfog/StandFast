using StandFast.Domain.Enums;

namespace StandFast.Application.Dtos;

/// <summary>Create/update input holding a date range. Both dates are null until picked, so a form can start empty; saving requires both.</summary>
public interface IDateRangeEdit
{
    DateOnly? StartDate { get; }

    DateOnly? EndDate { get; }
}

public sealed record HolidayDto(Guid Id, DateOnly Date, string Name, HolidayDuration Duration);

/// <summary>Create/update input for a holiday. <see cref="Id"/> is null when creating.</summary>
public sealed class HolidayEditDto
{
    public Guid? Id { get; set; }

    public DateOnly? Date { get; set; }

    public string Name { get; set; } = string.Empty;

    public HolidayDuration Duration { get; set; } = HolidayDuration.FullDay;
}

public sealed record SprintDto(Guid Id, Guid StandupId, string Name, DateOnly StartDate, DateOnly EndDate);

/// <summary>Create/update input for one standup's sprint. <see cref="Id"/> is null when creating.</summary>
public sealed class SprintEditDto : IDateRangeEdit
{
    public Guid? Id { get; set; }

    public Guid StandupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }
}

public sealed record DayOffDto(Guid Id, Guid PersonId, DateOnly StartDate, DateOnly EndDate, string? Note);

/// <summary>Create/update input for one person's days off. <see cref="Id"/> is null when creating.</summary>
public sealed class DayOffEditDto : IDateRangeEdit
{
    public Guid? Id { get; set; }

    public Guid PersonId { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Note { get; set; }
}

/// <summary>The sprint covering a board's date.</summary>
/// <param name="DaysLeft">Working days from the board's date through the sprint's last day, both included, less holidays; a half-day holiday takes off half a day.</param>
public sealed record SprintStatusDto(string Name, DateOnly StartDate, DateOnly EndDate, decimal DaysLeft);

/// <summary>How one person's days off bear on a board's date and the rest of its sprint.</summary>
/// <param name="IsOffOnDate">True when the person is off on the board's date, which keeps them from being marked present.</param>
/// <param name="OffNote">The note on the days off covering the board's date, when there is one.</param>
/// <param name="DaysOffInSprint">Working days the person is off from the board's date through the sprint's end. Null when no sprint covers the date.</param>
/// <param name="DaysLeftInSprint">Working days the person has left to contribute over the same span. Null when no sprint covers the date.</param>
public sealed record PersonAvailabilityDto(bool IsOffOnDate, string? OffNote, decimal? DaysOffInSprint, decimal? DaysLeftInSprint);
