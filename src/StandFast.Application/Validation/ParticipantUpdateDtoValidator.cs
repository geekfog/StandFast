using FluentValidation;
using StandFast.Application.Dtos;
using StandFast.Domain.Common;

namespace StandFast.Application.Validation;

public sealed class ParticipantUpdateDtoValidator : AbstractValidator<ParticipantUpdateDto>
{
    public ParticipantUpdateDtoValidator()
    {
        RuleFor(update => update.StandupId).NotEmpty();
        RuleFor(update => update.PersonId).NotEmpty();
        RuleFor(update => update.Update).MaximumLength(DomainLimits.MarkdownMaxLength);
        RuleFor(update => update.Blockers).MaximumLength(DomainLimits.MarkdownMaxLength);
    }
}
