using FluentValidation;
using StandFast.Application.Dtos;
using StandFast.Domain.Common;

namespace StandFast.Application.Validation;

/// <summary>Both ends picked, and the range running forward. Included by every validator whose input holds a date range.</summary>
public sealed class DateRangeEditValidator : AbstractValidator<IDateRangeEdit>
{
    public DateRangeEditValidator()
    {
        RuleFor(range => range.StartDate).NotNull().WithMessage(ValidationMessages.DatesRequired);
        RuleFor(range => range.EndDate).NotNull().WithMessage(ValidationMessages.DatesRequired);
        RuleFor(range => range.EndDate).Must((range, end) => end >= range.StartDate).WithMessage(ValidationMessages.EndBeforeStart).When(range => range.StartDate is not null && range.EndDate is not null);
    }
}

public sealed class HolidayEditDtoValidator : AbstractValidator<HolidayEditDto>
{
    public HolidayEditDtoValidator()
    {
        RuleFor(holiday => holiday.Date).NotNull().WithMessage(ValidationMessages.DateRequired);
        RuleFor(holiday => holiday.Name).NotEmpty().MaximumLength(DomainLimits.HolidayNameMaxLength);
        RuleFor(holiday => holiday.Duration).IsInEnum();
    }
}

public sealed class SprintEditDtoValidator : AbstractValidator<SprintEditDto>
{
    public SprintEditDtoValidator()
    {
        RuleFor(sprint => sprint.StandupId).NotEmpty();
        RuleFor(sprint => sprint.Name).NotEmpty().MaximumLength(DomainLimits.SprintNameMaxLength);
        RuleFor(sprint => sprint.Note).MaximumLength(DomainLimits.SprintNoteMaxLength);
        Include(new DateRangeEditValidator());
    }
}

public sealed class DayOffEditDtoValidator : AbstractValidator<DayOffEditDto>
{
    public DayOffEditDtoValidator()
    {
        RuleFor(dayOff => dayOff.PersonId).NotEmpty();
        RuleFor(dayOff => dayOff.Note).MaximumLength(DomainLimits.DayOffNoteMaxLength);
        Include(new DateRangeEditValidator());
    }
}
