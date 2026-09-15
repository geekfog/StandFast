using FluentValidation;
using StandFast.Application.Dtos;
using StandFast.Domain.Common;
using StandFast.Domain.Enums;

namespace StandFast.Application.Validation;

public sealed class StandupEditDtoValidator : AbstractValidator<StandupEditDto>
{
    public StandupEditDtoValidator()
    {
        RuleFor(standup => standup.Name).NotEmpty().MaximumLength(DomainLimits.StandupNameMaxLength);
        RuleFor(standup => standup.Description).MaximumLength(DomainLimits.DescriptionMaxLength);
        RuleFor(standup => standup.RecurrenceDays).NotEqual(MeetingDays.None).WithMessage("Select at least one day the standup runs on.");
        RuleFor(standup => standup.TimeZoneId).NotEmpty().MaximumLength(DomainLimits.TimeZoneIdMaxLength);
    }
}
