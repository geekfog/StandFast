using FluentValidation;
using StandFast.Application.Dtos;
using StandFast.Application.Services;
using StandFast.Application.Tests.Fakes;
using StandFast.Application.Validation;

namespace StandFast.Application.Tests.Services;

public sealed class SupervisorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 15, 0, 0, TimeSpan.Zero);

    private readonly InMemoryPersonRepository people = new();
    private readonly PersonService service;

    public SupervisorTests() => service = new PersonService(people, new FixedClock(Now), new PersonEditDtoValidator(), new RecordingAuditLog());

    [Fact]
    public async Task SavingAChain_StoresEachSupervisor()
    {
        Guid ada = await AddAsync("Ada", supervisorId: null);
        Guid ben = await AddAsync("Ben", ada);
        Guid cy = await AddAsync("Cy", ben);

        Assert.Equal(ben, (await service.GetForEditAsync(cy))!.SupervisorId);
        Assert.Equal(ada, (await service.GetAllAsync()).Single(person => person.Id == ben).SupervisorId);
    }

    [Fact]
    public async Task SavingSomeoneAsTheirOwnSupervisor_IsRefused()
    {
        Guid ada = await AddAsync("Ada", supervisorId: null);

        await AssertLoopRefusedAsync(ada, ada, ["Ada", "Ada"]);
    }

    [Fact]
    public async Task SavingASupervisorWhoReportsUpToThePerson_IsRefusedAndLeavesTheStoredChain()
    {
        Guid ada = await AddAsync("Ada", supervisorId: null);
        Guid ben = await AddAsync("Ben", ada);
        Guid cy = await AddAsync("Cy", ben);

        await AssertLoopRefusedAsync(ada, cy, ["Ada", "Cy", "Ben", "Ada"]);
        Assert.Null((await service.GetForEditAsync(ada))!.SupervisorId);
    }

    [Fact]
    public async Task SavingASupervisorNoLongerInPeople_IsRefused()
    {
        ValidationException refused = await Assert.ThrowsAsync<ValidationException>(() => AddAsync("Ada", Guid.CreateVersion7()));

        Assert.Contains(refused.Errors, error => error.PropertyName == nameof(PersonEditDto.SupervisorId) && error.ErrorMessage == ValidationMessages.SupervisorNotFound);
    }

    [Fact]
    public async Task DeletingASupervisor_LeavesTheirReportsWithNoSupervisor()
    {
        Guid ada = await AddAsync("Ada", supervisorId: null);
        Guid ben = await AddAsync("Ben", ada);

        await service.DeleteAsync(ada);

        Assert.Null((await service.GetForEditAsync(ben))!.SupervisorId);
    }

    private async Task AssertLoopRefusedAsync(Guid personId, Guid supervisorId, string[] expectedLoop)
    {
        PersonEditDto edit = (await service.GetForEditAsync(personId))!;
        edit.SupervisorId = supervisorId;

        ValidationException refused = await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(edit));

        Assert.Contains(refused.Errors, error => error.PropertyName == nameof(PersonEditDto.SupervisorId) && error.ErrorMessage == ValidationMessages.SupervisorLoop(expectedLoop));
    }

    private Task<Guid> AddAsync(string name, Guid? supervisorId) =>
        service.SaveAsync(new PersonEditDto { FirstName = name, LastName = "Lovelace", DisplayAs = name, Email = $"{name.ToLowerInvariant()}@example.com", SupervisorId = supervisorId });
}
