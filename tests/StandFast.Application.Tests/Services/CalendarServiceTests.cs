using FluentValidation;
using StandFast.Application.Dtos;
using StandFast.Application.Services;
using StandFast.Application.Tests.Fakes;
using StandFast.Application.Validation;
using StandFast.Domain.Entities;

namespace StandFast.Application.Tests.Services;

/// <summary>The rules for saving holidays, sprints, and days off that only what is already stored can check.</summary>
public sealed class CalendarServiceTests
{
    private static readonly DateOnly Monday = new(2026, 9, 14);

    private readonly InMemoryHolidayRepository holidays = new();
    private readonly InMemoryStandupRepository standups = new();
    private readonly InMemoryPersonRepository people = new();
    private readonly FixedClock clock = new(BoardTestContext.Now);

    [Fact]
    public async Task SavingASecondHolidayOnTheSameDate_IsRefused_ButEditingTheFirstIsNot()
    {
        HolidayService service = new(holidays, clock, new HolidayEditDtoValidator(), new RecordingAuditLog());
        Guid first = await service.SaveAsync(new HolidayEditDto { Date = Monday, Name = "Founders Day" });

        ValidationException refused = await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(new HolidayEditDto { Date = Monday, Name = "Other" }));
        await service.SaveAsync(new HolidayEditDto { Id = first, Date = Monday, Name = "Founders' Day" });

        Assert.Contains(refused.Errors, error => error.ErrorMessage == ValidationMessages.HolidayDateTaken("Founders Day"));
        Assert.Equal("Founders' Day", (await service.GetAllAsync()).Single().Name);
    }

    [Fact]
    public async Task SavingASprintThatOverlapsAnother_IsRefused_ButOneThatFollowsItIsNot()
    {
        Guid standupId = Guid.CreateVersion7();
        SprintService service = new(standups, clock, new SprintEditDtoValidator(), new RecordingAuditLog());
        await service.SaveAsync(new SprintEditDto { StandupId = standupId, Name = "Sprint 28", StartDate = Monday, EndDate = Monday.AddDays(11) });

        ValidationException refused = await Assert.ThrowsAsync<ValidationException>(
            () => service.SaveAsync(new SprintEditDto { StandupId = standupId, Name = "Sprint 29", StartDate = Monday.AddDays(11), EndDate = Monday.AddDays(25) }));
        await service.SaveAsync(new SprintEditDto { StandupId = standupId, Name = "Sprint 29", StartDate = Monday.AddDays(14), EndDate = Monday.AddDays(25) });

        Assert.Contains(refused.Errors, error => error.ErrorMessage == ValidationMessages.SprintOverlap("Sprint 28"));
        Assert.Equal(["Sprint 29", "Sprint 28"], (await service.GetForStandupAsync(standupId)).Select(sprint => sprint.Name));
    }

    [Fact]
    public async Task SavingDaysOffThatEndBeforeTheyStart_IsRefused()
    {
        DayOffService service = new(people, clock, new DayOffEditDtoValidator(), new RecordingAuditLog());

        ValidationException refused = await Assert.ThrowsAsync<ValidationException>(
            () => service.SaveAsync(new DayOffEditDto { PersonId = Guid.CreateVersion7(), StartDate = Monday, EndDate = Monday.AddDays(-1) }));

        Assert.Contains(refused.Errors, error => error.ErrorMessage == ValidationMessages.EndBeforeStart);
    }

    [Fact]
    public async Task RemovingAPerson_RemovesTheirDaysOff()
    {
        Person ada = new() { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" };
        await people.UpsertAsync(ada);
        DayOffService service = new(people, clock, new DayOffEditDtoValidator(), new RecordingAuditLog());
        await service.SaveAsync(new DayOffEditDto { PersonId = ada.Id, StartDate = Monday, EndDate = Monday, Note = "  " });

        Assert.Null((await service.GetForPersonAsync(ada.Id)).Single().Note);

        await new PersonService(people, clock, new PersonEditDtoValidator(), new RecordingAuditLog()).DeleteAsync(ada.Id);

        Assert.Empty(await service.GetForPersonAsync(ada.Id));
    }
}
