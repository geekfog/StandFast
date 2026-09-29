using StandFast.Application.Dtos;
using StandFast.Application.Validation;

namespace StandFast.Application.Tests.Validation;

public sealed class PersonEditDtoValidatorTests
{
    private readonly PersonEditDtoValidator validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("America/Chicago")]
    public void TimeZone_AcceptsNoneOrAKnownZone(string? timeZoneId)
    {
        Assert.True(validator.Validate(ValidPerson(timeZoneId)).IsValid);
    }

    [Fact]
    public void TimeZone_RejectsAnUnknownZone()
    {
        FluentValidation.Results.ValidationResult result = validator.Validate(ValidPerson("Mars/Olympus_Mons"));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(PersonEditDto.TimeZoneId) && error.ErrorMessage == ValidationMessages.UnknownTimeZone);
    }

    private static PersonEditDto ValidPerson(string? timeZoneId) => new() { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com", TimeZoneId = timeZoneId };
}
