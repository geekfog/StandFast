using FluentValidation;
using StandFast.Application.Dtos;
using StandFast.Application.Services;
using StandFast.Application.Tests.Fakes;
using StandFast.Application.Validation;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Services;

/// <summary>Task states, and the tasks on the signed-in user's own list, which is found by matching their address to People.</summary>
public sealed class TaskServiceTests
{
    private const string AdaEmail = "ada@example.com";

    private readonly InMemoryTaskRepository tasks = new();
    private readonly InMemoryPersonRepository people = new();
    private readonly FixedClock clock = new(BoardTestContext.Now);
    private readonly TaskStateService states;
    private readonly TaskService service;
    private readonly Person ada = new() { FirstName = "Ada", LastName = "Lovelace", Email = AdaEmail };
    private readonly Person grace = new() { FirstName = "Grace", LastName = "Hopper", Email = "grace@example.com" };

    public TaskServiceTests()
    {
        states = new TaskStateService(tasks, clock, new TaskStateEditDtoValidator(), new RecordingAuditLog());
        service = new TaskService(tasks, people, clock, new WorkTaskEditDtoValidator(), new RecordingAuditLog());
    }

    private async Task<Guid> SetUpAsync(TaskStateGroup group = TaskStateGroup.Review)
    {
        await people.UpsertAsync(ada);
        await people.UpsertAsync(grace);
        return await states.SaveAsync(new TaskStateEditDto { Title = group.ToString(), Group = group });
    }

    private WorkTaskEditDto NewTask(Guid stateId, string title, int priority, DateOnly? dueDate = null) =>
        new() { Title = title, StateId = stateId, Priority = priority, DueDate = dueDate, RequestedByPersonId = grace.Id };

    [Fact]
    public async Task TheList_SortsByPriority_ThenLatestDueDateFirst_WithUndatedTasksLast()
    {
        Guid stateId = await SetUpAsync();
        await service.SaveAsync(AdaEmail, NewTask(stateId, "Undated", 1));
        await service.SaveAsync(AdaEmail, NewTask(stateId, "Earlier", 1, new DateOnly(2026, 12, 1)));
        await service.SaveAsync(AdaEmail, NewTask(stateId, "Later", 1, new DateOnly(2026, 12, 9)));
        await service.SaveAsync(AdaEmail, NewTask(stateId, "Less urgent", 2, new DateOnly(2027, 1, 1)));

        TaskListDto list = (await service.GetForOwnerAsync(AdaEmail))!;

        Assert.Equal(["Later", "Earlier", "Undated", "Less urgent"], list.Tasks.Select(task => task.Title));
        Assert.Equal(ada.Id, list.OwnerPersonId);
        Assert.Equal(grace.Id, list.Tasks[0].RequestedByPersonId);
    }

    [Fact]
    public async Task ATaskInADoneState_IsDone_AndDatedByItsLastUpdate()
    {
        Guid doneId = await SetUpAsync(TaskStateGroup.Done);
        await service.SaveAsync(AdaEmail, NewTask(doneId, "Shipped", 5));

        WorkTaskDto task = (await service.GetForOwnerAsync(AdaEmail))!.Tasks.Single();

        Assert.True(task.IsDone);
        Assert.Equal(clock.UtcNow, task.LastUpdatedUtc);
    }

    [Fact]
    public async Task DoneTasks_FollowOpenOnes_MostRecentlyUpdatedFirst_WhateverTheirPriority()
    {
        Guid openId = await SetUpAsync();
        Guid doneId = await states.SaveAsync(new TaskStateEditDto { Title = "Closed", Group = TaskStateGroup.Done });
        await service.SaveAsync(AdaEmail, NewTask(doneId, "Done first", 1));
        await service.SaveAsync(AdaEmail, NewTask(openId, "Open", 50));
        clock.UtcNow = clock.UtcNow.AddDays(1);
        await service.SaveAsync(AdaEmail, NewTask(doneId, "Done later", 90));

        TaskListDto list = (await service.GetForOwnerAsync(AdaEmail))!;

        Assert.Equal(["Open", "Done later", "Done first"], list.Tasks.Select(task => task.Title));
    }

    [Fact]
    public async Task AnAddressNobodyInPeopleHas_HasNoList_AndCannotSaveATask()
    {
        Guid stateId = await SetUpAsync();

        Assert.Null(await service.GetForOwnerAsync("stranger@example.com"));
        ValidationException refused = await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync("stranger@example.com", NewTask(stateId, "Anything", 1)));
        Assert.Contains(refused.Errors, error => error.ErrorMessage == ValidationMessages.TaskOwnerNotFound);
    }

    [Fact]
    public async Task ATaskOnSomeoneElsesList_CannotBeEditedThroughYours()
    {
        Guid stateId = await SetUpAsync();
        Guid gracesTask = (await service.SaveAsync(grace.Email, NewTask(stateId, "Grace's own", 1))).Single();

        WorkTaskEditDto hijack = NewTask(stateId, "Renamed", 1);
        hijack.Id = gracesTask;

        await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(AdaEmail, hijack));
        Assert.Equal("Grace's own", (await service.GetForOwnerAsync(grace.Email))!.Tasks.Single().Title);
    }

    [Fact]
    public async Task AddingSeveralTitles_AddsOneTaskPerNonBlankLine_EachWithTheSameSettings()
    {
        Guid stateId = await SetUpAsync();
        DateOnly due = new(2026, 12, 4);

        IReadOnlyList<Guid> added = await service.SaveAsync(AdaEmail, NewTask(stateId, "  First \r\n\n   \nSecond\n", 7, due));

        IReadOnlyList<WorkTaskDto> list = (await service.GetForOwnerAsync(AdaEmail))!.Tasks;
        Assert.Equal(2, added.Count);
        Assert.Equal(["First", "Second"], list.Select(task => task.Title));
        Assert.All(list, task => Assert.Equal((stateId, 7, (DateOnly?)due, grace.Id), (task.StateId, task.Priority, task.DueDate, task.RequestedByPersonId)));
    }

    [Fact]
    public async Task EditingATask_KeepsItToOneTitle_AndEachAddedTitleToTheLengthLimit()
    {
        Guid stateId = await SetUpAsync();
        Guid taskId = (await service.SaveAsync(AdaEmail, NewTask(stateId, "Only", 1))).Single();

        WorkTaskEditDto split = NewTask(stateId, "One\nTwo", 1);
        split.Id = taskId;
        ValidationException refused = await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(AdaEmail, split));
        ValidationException tooLong = await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(AdaEmail, NewTask(stateId, $"Fine\n{new string('x', 201)}", 1)));

        Assert.Contains(refused.Errors, error => error.ErrorMessage == ValidationMessages.TaskTitleSingle);
        Assert.Contains(tooLong.Errors, error => error.ErrorMessage == ValidationMessages.TaskTitleTooLong);
        Assert.Equal(["Only"], (await service.GetForOwnerAsync(AdaEmail))!.Tasks.Select(task => task.Title));
    }

    [Fact]
    public async Task APriorityOutsideTheRange_IsRefused()
    {
        Guid stateId = await SetUpAsync();

        await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(AdaEmail, NewTask(stateId, "Too urgent", 0)));
        await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(AdaEmail, NewTask(stateId, "Too lax", 101)));
    }

    [Fact]
    public async Task AStateTitleAlreadyTaken_IsRefused_WhateverItsCase()
    {
        await states.SaveAsync(new TaskStateEditDto { Title = "Blocked", Group = TaskStateGroup.Stopped });

        ValidationException refused = await Assert.ThrowsAsync<ValidationException>(() => states.SaveAsync(new TaskStateEditDto { Title = " blocked ", Group = TaskStateGroup.Review }));

        Assert.Contains(refused.Errors, error => error.ErrorMessage == ValidationMessages.TaskStateTitleTaken("Blocked"));
    }

    [Fact]
    public async Task AStateATaskIsIn_CannotBeRemoved_UntilTheTaskMovesOut()
    {
        Guid stateId = await SetUpAsync();
        Guid otherId = await states.SaveAsync(new TaskStateEditDto { Title = "Closed", Group = TaskStateGroup.Done });
        Guid taskId = (await service.SaveAsync(AdaEmail, NewTask(stateId, "Pending", 1))).Single();

        await Assert.ThrowsAsync<ValidationException>(() => states.DeleteAsync(stateId));

        WorkTaskEditDto moved = NewTask(otherId, "Pending", 1);
        moved.Id = taskId;
        await service.SaveAsync(AdaEmail, moved);
        await states.DeleteAsync(stateId);

        Assert.Equal([otherId], (await states.GetAllAsync()).Select(state => state.Id));
    }

    [Fact]
    public async Task StatesAreListed_ByGroupInWorkOrder_ThenByTitle()
    {
        await states.SaveAsync(new TaskStateEditDto { Title = "Closed", Group = TaskStateGroup.Done });
        await states.SaveAsync(new TaskStateEditDto { Title = "New", Group = TaskStateGroup.Review });
        await states.SaveAsync(new TaskStateEditDto { Title = "Active", Group = TaskStateGroup.InFlight });
        await states.SaveAsync(new TaskStateEditDto { Title = "Hold", Group = TaskStateGroup.Review });

        Assert.Equal(["Hold", "New", "Active", "Closed"], (await states.GetAllAsync()).Select(state => state.Title));
    }

    [Fact]
    public async Task RemovingAPerson_RemovesTheirOwnList_AndLeavesTasksTheyRequestedOnOthersLists()
    {
        Guid stateId = await SetUpAsync();
        await service.SaveAsync(grace.Email, NewTask(stateId, "Grace's own", 1));
        await service.SaveAsync(AdaEmail, NewTask(stateId, "Asked by Grace", 1));

        await new PersonService(people, tasks, clock, new PersonEditDtoValidator(), new RecordingAuditLog()).DeleteAsync(grace.Id);

        Assert.Empty(await tasks.GetForOwnerAsync(grace.Id));
        Assert.Equal(grace.Id, (await service.GetForOwnerAsync(AdaEmail))!.Tasks.Single().RequestedByPersonId);
    }
}
