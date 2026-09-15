using FluentValidation;
using StandFast.Application.Dtos;
using StandFast.Domain.Common;

namespace StandFast.Application.Validation;

public sealed class PersonEditDtoValidator : AbstractValidator<PersonEditDto>
{
    public PersonEditDtoValidator()
    {
        RuleFor(person => person.FirstName).NotEmpty().MaximumLength(DomainLimits.PersonNameMaxLength);
        RuleFor(person => person.LastName).NotEmpty().MaximumLength(DomainLimits.PersonNameMaxLength);
        RuleFor(person => person.Email).NotEmpty().MaximumLength(DomainLimits.EmailMaxLength).EmailAddress();
    }
}
