using FluentValidation;
using StandFast.Application.Auditing;
using StandFast.Application.Dtos;
using StandFast.Application.Mapping;
using StandFast.Application.Validation;
using StandFast.Domain.Abstractions;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;

namespace StandFast.Application.Services;

public interface IHolidayService
{
    /// <summary>Every holiday, earliest first.</summary>
    Task<IReadOnlyList<HolidayDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Refused when another holiday is already recorded on the same date.</summary>
    Task<Guid> SaveAsync(HolidayEditDto holiday, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface ISprintService
{
    /// <summary>One standup's sprints, latest first.</summary>
    Task<IReadOnlyList<SprintDto>> GetForStandupAsync(Guid standupId, CancellationToken cancellationToken = default);

    /// <summary>Refused when the dates overlap another of the standup's sprints.</summary>
    Task<Guid> SaveAsync(SprintEditDto sprint, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid standupId, Guid sprintId, CancellationToken cancellationToken = default);
}

public interface IDayOffService
{
    /// <summary>One person's days off, latest first.</summary>
    Task<IReadOnlyList<DayOffDto>> GetForPersonAsync(Guid personId, CancellationToken cancellationToken = default);

    Task<Guid> SaveAsync(DayOffEditDto dayOff, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid personId, Guid dayOffId, CancellationToken cancellationToken = default);
}

public sealed class HolidayService(IHolidayRepository holidays, IClock clock, IValidator<HolidayEditDto> validator, IAuditLog audit) : IHolidayService
{
    private const string AuditTargetType = nameof(Holiday);

    public async Task<IReadOnlyList<HolidayDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        [.. (await holidays.GetAllAsync(cancellationToken)).OrderBy(holiday => holiday.Date).Select(holiday => holiday.ToDto())];

    public async Task<Guid> SaveAsync(HolidayEditDto holiday, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(holiday, cancellationToken);

        IReadOnlyList<Holiday> stored = await holidays.GetAllAsync(cancellationToken);
        if (stored.FirstOrDefault(other => other.Id != holiday.Id && other.Date == holiday.Date) is { } taken)
        {
            throw ValidationMessages.Failure(nameof(HolidayEditDto.Date), ValidationMessages.HolidayDateTaken(taken.Name));
        }

        Holiday? existing = stored.FirstOrDefault(other => other.Id == holiday.Id);
        Holiday entity = existing ?? new Holiday { CreatedUtc = clock.UtcNow };
        holiday.ApplyTo(entity);
        entity.ModifiedUtc = existing is null ? null : clock.UtcNow;

        await holidays.UpsertAsync(entity, cancellationToken);
        audit.Record(existing is null ? AuditEvents.HolidayCreated : AuditEvents.HolidayUpdated, AuditTargetType, entity.Id.ToString(), $"{entity.Name} on {MeetingCalendar.ToRouteValue(entity.Date)}");

        return entity.Id;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await holidays.GetAsync(id, cancellationToken) is not { } existing)
        {
            return;
        }

        await holidays.DeleteAsync(id, cancellationToken);
        audit.Record(AuditEvents.HolidayDeleted, AuditTargetType, id.ToString(), $"{existing.Name} on {MeetingCalendar.ToRouteValue(existing.Date)}");
    }
}

public sealed class SprintService(IStandupRepository standups, IClock clock, IValidator<SprintEditDto> validator, IAuditLog audit) : ISprintService
{
    private const string AuditTargetType = nameof(Sprint);

    public async Task<IReadOnlyList<SprintDto>> GetForStandupAsync(Guid standupId, CancellationToken cancellationToken = default) =>
        [.. (await standups.GetSprintsAsync(standupId, cancellationToken)).OrderByDescending(sprint => sprint.StartDate).Select(sprint => sprint.ToDto())];

    public async Task<Guid> SaveAsync(SprintEditDto sprint, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(sprint, cancellationToken);

        IReadOnlyList<Sprint> stored = await standups.GetSprintsAsync(sprint.StandupId, cancellationToken);
        if (stored.FirstOrDefault(other => other.Id != sprint.Id && other.Dates.Overlaps(sprint.ToDateRange())) is { } overlapped)
        {
            throw ValidationMessages.Failure(nameof(SprintEditDto.StartDate), ValidationMessages.SprintOverlap(overlapped.Name));
        }

        Sprint? existing = stored.FirstOrDefault(other => other.Id == sprint.Id);
        Sprint entity = existing ?? new Sprint { CreatedUtc = clock.UtcNow };
        sprint.ApplyTo(entity);
        entity.ModifiedUtc = existing is null ? null : clock.UtcNow;

        await standups.UpsertSprintAsync(entity, cancellationToken);
        audit.Record(existing is null ? AuditEvents.SprintCreated : AuditEvents.SprintUpdated, AuditTargetType, entity.Id.ToString(), entity.Name);

        return entity.Id;
    }

    public async Task DeleteAsync(Guid standupId, Guid sprintId, CancellationToken cancellationToken = default)
    {
        if ((await standups.GetSprintsAsync(standupId, cancellationToken)).FirstOrDefault(sprint => sprint.Id == sprintId) is not { } existing)
        {
            return;
        }

        await standups.DeleteSprintAsync(standupId, sprintId, cancellationToken);
        audit.Record(AuditEvents.SprintDeleted, AuditTargetType, sprintId.ToString(), existing.Name);
    }
}

public sealed class DayOffService(IPersonRepository people, IClock clock, IValidator<DayOffEditDto> validator, IAuditLog audit) : IDayOffService
{
    private const string AuditTargetType = nameof(PersonDayOff);

    public async Task<IReadOnlyList<DayOffDto>> GetForPersonAsync(Guid personId, CancellationToken cancellationToken = default) =>
        [.. (await people.GetDaysOffAsync(personId, cancellationToken)).OrderByDescending(dayOff => dayOff.StartDate).Select(dayOff => dayOff.ToDto())];

    public async Task<Guid> SaveAsync(DayOffEditDto dayOff, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(dayOff, cancellationToken);

        PersonDayOff? existing = (await people.GetDaysOffAsync(dayOff.PersonId, cancellationToken)).FirstOrDefault(other => other.Id == dayOff.Id);
        PersonDayOff entity = existing ?? new PersonDayOff { CreatedUtc = clock.UtcNow };
        dayOff.ApplyTo(entity);
        entity.ModifiedUtc = existing is null ? null : clock.UtcNow;

        await people.UpsertDayOffAsync(entity, cancellationToken);
        audit.Record(existing is null ? AuditEvents.DayOffCreated : AuditEvents.DayOffUpdated, AuditTargetType, entity.Id.ToString(), Describe(entity));

        return entity.Id;
    }

    public async Task DeleteAsync(Guid personId, Guid dayOffId, CancellationToken cancellationToken = default)
    {
        if ((await people.GetDaysOffAsync(personId, cancellationToken)).FirstOrDefault(dayOff => dayOff.Id == dayOffId) is not { } existing)
        {
            return;
        }

        await people.DeleteDayOffAsync(personId, dayOffId, cancellationToken);
        audit.Record(AuditEvents.DayOffDeleted, AuditTargetType, dayOffId.ToString(), Describe(existing));
    }

    private static string Describe(PersonDayOff dayOff) =>
        $"{dayOff.PersonId}: {MeetingCalendar.ToRouteValue(dayOff.StartDate)} to {MeetingCalendar.ToRouteValue(dayOff.EndDate)}";
}
