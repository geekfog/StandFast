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
        RuleFor(person => person.DisplayAs).MaximumLength(DomainLimits.DisplayAsMaxLength);
        RuleFor(person => person.Notes).MaximumLength(DomainLimits.MarkdownMaxLength);
        RuleFor(person => person.Title).MaximumLength(DomainLimits.TitleMaxLength);
        RuleFor(person => person.Organization).MaximumLength(DomainLimits.OrganizationMaxLength);
        RuleFor(person => person.Department).MaximumLength(DomainLimits.DepartmentMaxLength);
        RuleFor(person => person.Photo!.Content.Length).LessThanOrEqualTo(DomainLimits.PhotoMaxBytes).WithMessage(ValidationMessages.PhotoTooLarge).When(person => person.Photo is not null);
        RuleFor(person => person.Photo!.ContentType).Equal(PhotoFormat.ContentType).When(person => person.Photo is not null);
        RuleFor(person => person.City).MaximumLength(DomainLimits.CityMaxLength);
        RuleFor(person => person.StateOrRegion).MaximumLength(DomainLimits.StateOrRegionMaxLength);
        RuleFor(person => person.TimeZoneId)
            .MaximumLength(DomainLimits.TimeZoneIdMaxLength)
            .Must(TimeZoneIds.IsKnown).WithMessage(ValidationMessages.UnknownTimeZone)
            .When(person => !string.IsNullOrWhiteSpace(person.TimeZoneId));
    }
}
