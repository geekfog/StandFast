using StandFast.Application.Dtos;
using StandFast.Application.Services;
using StandFast.Application.Tests.Fakes;
using StandFast.Application.Validation;
using StandFast.Domain.Common;

namespace StandFast.Application.Tests.Services;

public sealed class PersonPhotoTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 15, 0, 0, TimeSpan.Zero);
    private static readonly PersonPhotoDto Photo = new(PhotoFormat.ContentType, [1, 2, 3]);

    private readonly InMemoryPersonRepository people = new();
    private readonly PersonService service;

    public PersonPhotoTests() => service = new PersonService(people, new FixedClock(Now), new PersonEditDtoValidator(), new RecordingAuditLog());

    [Fact]
    public async Task SavingWithAPhoto_StoresItAndStampsTheSaveTime()
    {
        Guid id = await service.SaveAsync(ValidPerson(Photo, isPhotoChanged: true));

        PersonEditDto? edit = await service.GetForEditAsync(id);
        Assert.Equal(Photo.Content, edit?.Photo?.Content);
        Assert.Equal(Now, (await service.GetAllAsync()).Single().PhotoSavedUtc);
    }

    [Fact]
    public async Task RemovingThePhoto_DeletesItAndClearsTheSaveTime()
    {
        Guid id = await service.SaveAsync(ValidPerson(Photo, isPhotoChanged: true));

        PersonEditDto edit = (await service.GetForEditAsync(id))!;
        edit.Photo = null;
        edit.IsPhotoChanged = true;
        await service.SaveAsync(edit);

        Assert.Null(await service.GetPhotoAsync(id));
        Assert.Null((await service.GetAllAsync()).Single().PhotoSavedUtc);
    }

    [Fact]
    public async Task SavingWithoutAPhotoChange_KeepsTheStoredPhoto()
    {
        Guid id = await service.SaveAsync(ValidPerson(Photo, isPhotoChanged: true));

        PersonEditDto edit = (await service.GetForEditAsync(id))!;
        edit.Photo = null;
        edit.FirstName = "Augusta";
        await service.SaveAsync(edit);

        Assert.NotNull(await service.GetPhotoAsync(id));
        Assert.Equal(Now, (await service.GetAllAsync()).Single().PhotoSavedUtc);
    }

    [Fact]
    public async Task DeletingThePerson_DeletesTheirPhoto()
    {
        Guid id = await service.SaveAsync(ValidPerson(Photo, isPhotoChanged: true));

        await service.DeleteAsync(id);

        Assert.Null(await service.GetPhotoAsync(id));
    }

    [Fact]
    public void Validation_RejectsAPhotoOverTheStorageLimit()
    {
        PersonEditDto person = ValidPerson(new PersonPhotoDto(PhotoFormat.ContentType, new byte[DomainLimits.PhotoMaxBytes + 1]), isPhotoChanged: true);

        FluentValidation.Results.ValidationResult result = new PersonEditDtoValidator().Validate(person);

        Assert.Contains(result.Errors, error => error.ErrorMessage == ValidationMessages.PhotoTooLarge);
    }

    private static PersonEditDto ValidPerson(PersonPhotoDto? photo, bool isPhotoChanged) =>
        new() { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com", Photo = photo, IsPhotoChanged = isPhotoChanged };
}
