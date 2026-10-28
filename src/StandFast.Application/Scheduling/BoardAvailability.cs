using StandFast.Application.Dtos;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Scheduling;

/// <summary>What a board shows about the sprint covering its date and about who is away, counted from the board's date, which is included.</summary>
public static class BoardAvailability
{
    /// <summary>The sprint covering <paramref name="date"/>, with the working days left in it, or null when no sprint covers the date.</summary>
    public static SprintStatusDto? ForSprint(IEnumerable<Sprint> sprints, DateOnly date, IReadOnlyDictionary<DateOnly, HolidayDuration> holidays) =>
        sprints.FirstOrDefault(sprint => sprint.Dates.Contains(date)) is { } sprint
            ? new SprintStatusDto(sprint.Name, sprint.StartDate, sprint.EndDate, WorkCalendar.WorkingDays(new DateRange(date, sprint.EndDate), holidays), sprint.Note)
            : null;

    /// <summary>The dates whose days off matter to the board: the rest of the sprint, or the board's date alone when no sprint covers it.</summary>
    public static DateRange Period(DateOnly date, SprintStatusDto? sprint) => new(date, sprint?.EndDate ?? date);

    /// <summary>
    /// Availability for each person whose days off touch the board: off on its date, or off on a working day in the rest of its sprint. Anyone else is left
    /// out, since their availability matches the sprint's.
    /// </summary>
    public static IReadOnlyDictionary<Guid, PersonAvailabilityDto> ForPeople(
        IEnumerable<PersonDayOff> daysOff, DateOnly date, SprintStatusDto? sprint, IReadOnlyDictionary<DateOnly, HolidayDuration> holidays) =>
        daysOff
            .GroupBy(dayOff => dayOff.PersonId)
            .Select(group => (PersonId: group.Key, Availability: ForPerson([.. group], date, sprint, holidays)))
            .Where(item => item.Availability is not null)
            .ToDictionary(item => item.PersonId, item => item.Availability!);

    private static PersonAvailabilityDto? ForPerson(IReadOnlyList<PersonDayOff> daysOff, DateOnly date, SprintStatusDto? sprint, IReadOnlyDictionary<DateOnly, HolidayDuration> holidays)
    {
        PersonDayOff? offOnDate = daysOff.FirstOrDefault(dayOff => dayOff.Dates.Contains(date));
        decimal? daysLeft = sprint is null ? null : WorkCalendar.WorkingDays(Period(date, sprint), holidays, day => daysOff.Any(dayOff => dayOff.Dates.Contains(day)));
        decimal? daysOffInSprint = sprint?.DaysLeft - daysLeft;

        return offOnDate is null && !(daysOffInSprint > 0) ? null : new PersonAvailabilityDto(offOnDate is not null, offOnDate?.Note, daysOffInSprint, daysLeft);
    }
}
