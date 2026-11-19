using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Infrastructure.Tests.Integration;

/// <summary>Round-trips task states and tasks through Azurite, including the cross-owner query that keeps a state in use from being deleted.</summary>
public sealed class TaskRepositoryTests : IClassFixture<AzuriteTableFixture>
{
    private readonly AzuriteTableFixture fixture;

    public TaskRepositoryTests(AzuriteTableFixture fixture) => this.fixture = fixture;

    [AzuriteFact]
    public async Task States_RoundTripAndDelete()
    {
        TaskState state = new() { Title = "Blocked", Group = TaskStateGroup.Stopped, CreatedUtc = DateTimeOffset.UtcNow };

        await fixture.Tasks.UpsertStateAsync(state);
        Assert.Equal(TaskStateGroup.Stopped, (await fixture.Tasks.GetStateAsync(state.Id))!.Group);
        Assert.Contains(await fixture.Tasks.GetStatesAsync(), stored => stored.Id == state.Id);

        await fixture.Tasks.DeleteStateAsync(state.Id);
        Assert.Null(await fixture.Tasks.GetStateAsync(state.Id));
    }

    [AzuriteFact]
    public async Task Tasks_AreReadPerOwner_AndAStateIsInUseWhileAnyOwnersTaskIsInIt()
    {
        Guid stateId = Guid.CreateVersion7();
        WorkTask mine = await AddTaskAsync(Guid.CreateVersion7(), stateId, new DateOnly(2026, 12, 4));
        WorkTask theirs = await AddTaskAsync(Guid.CreateVersion7(), stateId, dueDate: null);

        Assert.Equal([mine.Id], (await fixture.Tasks.GetForOwnerAsync(mine.OwnerPersonId)).Select(task => task.Id));
        Assert.Equal(mine.DueDate, (await fixture.Tasks.GetAsync(mine.OwnerPersonId, mine.Id))!.DueDate);
        Assert.Null(await fixture.Tasks.GetAsync(mine.OwnerPersonId, theirs.Id));
        Assert.True(await fixture.Tasks.IsStateInUseAsync(stateId));

        await fixture.Tasks.DeleteAsync(mine.OwnerPersonId, mine.Id);
        Assert.True(await fixture.Tasks.IsStateInUseAsync(stateId));

        await fixture.Tasks.DeleteAsync(theirs.OwnerPersonId, theirs.Id);
        Assert.False(await fixture.Tasks.IsStateInUseAsync(stateId));
    }

    private async Task<WorkTask> AddTaskAsync(Guid ownerPersonId, Guid stateId, DateOnly? dueDate)
    {
        WorkTask task = new()
        {
            OwnerPersonId = ownerPersonId,
            Title = "Draft the runbook",
            StateId = stateId,
            Priority = 3,
            DueDate = dueDate,
            RequestedByPersonId = Guid.CreateVersion7(),
            CreatedUtc = DateTimeOffset.UtcNow,
        };

        await fixture.Tasks.UpsertAsync(task);
        return task;
    }
}
