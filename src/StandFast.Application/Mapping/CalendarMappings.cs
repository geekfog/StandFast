using StandFast.Application.Dtos;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Mapping;

/// <summary>The only place holidays, sprints, and days off are converted to and from their DTOs, including input normalization.</summary>
public static class CalendarMappings
{
    public static HolidayDto ToDto(this Holiday holiday) => new(holiday.Id, holiday.Date, holiday.Name, holiday.Duration);

    public static HolidayEditDto ToEditDto(this HolidayDto holiday) => new() { Id = holiday.Id, Date = holiday.Date, Name = holiday.Name, Duration = holiday.Duration };

    /// <summary>Called only after validation, which guarantees the date is set.</summary>
    public static void ApplyTo(this HolidayEditDto dto, Holiday holiday)
    {
        holiday.Date = dto.Date!.Value;
        holiday.Name = dto.Name.Trim();
        holiday.Duration = dto.Duration;
    }

    /// <summary>How much of each holiday date is taken off, which is what working-day counts read. Only one holiday is kept per date.</summary>
    public static IReadOnlyDictionary<DateOnly, HolidayDuration> ToDurationByDate(this IEnumerable<Holiday> holidays) =>
        holidays.DistinctBy(holiday => holiday.Date).ToDictionary(holiday => holiday.Date, holiday => holiday.Duration);

    public static SprintDto ToDto(this Sprint sprint) => new(sprint.Id, sprint.StandupId, sprint.Name, sprint.StartDate, sprint.EndDate);

    public static SprintEditDto ToEditDto(this SprintDto sprint) =>
        new() { Id = sprint.Id, StandupId = sprint.StandupId, Name = sprint.Name, StartDate = sprint.StartDate, EndDate = sprint.EndDate };

    /// <summary>Called only after validation, which guarantees both dates are set.</summary>
    public static void ApplyTo(this SprintEditDto dto, Sprint sprint)
    {
        sprint.StandupId = dto.StandupId;
        sprint.Name = dto.Name.Trim();
        sprint.StartDate = dto.StartDate!.Value;
        sprint.EndDate = dto.EndDate!.Value;
    }

    public static DayOffDto ToDto(this PersonDayOff dayOff) => new(dayOff.Id, dayOff.PersonId, dayOff.StartDate, dayOff.EndDate, dayOff.Note);

    public static DayOffEditDto ToEditDto(this DayOffDto dayOff) =>
        new() { Id = dayOff.Id, PersonId = dayOff.PersonId, StartDate = dayOff.StartDate, EndDate = dayOff.EndDate, Note = dayOff.Note };

    /// <summary>Called only after validation, which guarantees both dates are set.</summary>
    public static void ApplyTo(this DayOffEditDto dto, PersonDayOff dayOff)
    {
        dayOff.PersonId = dto.PersonId;
        dayOff.StartDate = dto.StartDate!.Value;
        dayOff.EndDate = dto.EndDate!.Value;
        dayOff.Note = OptionalText.Normalize(dto.Note);
    }

    /// <summary>The range an edit describes. Called only after validation, which guarantees both dates are set.</summary>
    public static DateRange ToDateRange(this IDateRangeEdit dto) => new(dto.StartDate!.Value, dto.EndDate!.Value);
}
