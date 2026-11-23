using StandFast.Application.Dtos;
using StandFast.Application.Mapping;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Mapping;

public sealed class TaskDeadlineConflictTests
{
    private static readonly DateOnly Today = new(2026, 11, 23);
    private static readonly DateTimeOffset Created = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LessUrgentTaskDueSooner_IsCountedOncePerMoreUrgentTaskDueLater()
    {
        WorkTaskDto urgentLate = Task(priority: 1, due: Today.AddDays(10));
        WorkTaskDto alsoUrgentLate = Task(priority: 2, due: Today.AddDays(12));
        WorkTaskDto minorSoon = Task(priority: 50, due: Today.AddDays(1));

        IReadOnlyDictionary<Guid, int> conflicts = new[] { urgentLate, alsoUrgentLate, minorSoon }.DueAheadOfMoreUrgent();

        Assert.Equal(2, Assert.Single(conflicts, conflict => conflict.Key == minorSoon.Id).Value);
        Assert.Single(conflicts);
    }

    [Fact]
    public void TasksWhosePriorityAndDueDateAgree_AreNotFlagged()
    {
        WorkTaskDto[] tasks = [Task(priority: 1, due: Today), Task(priority: 5, due: Today.AddDays(3)), Task(priority: 9, due: Today.AddDays(7))];

        Assert.Empty(tasks.DueAheadOfMoreUrgent());
    }

    [Fact]
    public void EqualPriorities_AreNotAConflict()
    {
        WorkTaskDto[] tasks = [Task(priority: 3, due: Today.AddDays(9)), Task(priority: 3, due: Today)];

        Assert.Empty(tasks.DueAheadOfMoreUrgent());
    }

    [Fact]
    public void UndatedTasks_AreIgnoredOnBothSides()
    {
        WorkTaskDto[] tasks = [Task(priority: 1, due: null), Task(priority: 50, due: Today), Task(priority: 90, due: null)];

        Assert.Empty(tasks.DueAheadOfMoreUrgent());
    }

    [Fact]
    public void DoneTasks_AreIgnoredOnBothSides()
    {
        WorkTaskDto[] tasks =
        [
            Task(priority: 1, due: Today.AddDays(10), group: TaskStateGroup.Done),
            Task(priority: 50, due: Today),
            Task(priority: 90, due: Today.AddDays(-1), group: TaskStateGroup.Done),
        ];

        Assert.Empty(tasks.DueAheadOfMoreUrgent());
    }

    [Fact]
    public void ListOrder_PutsFlaggedTasksFirstBySoonestDueDate_ThenTheRestByPriority()
    {
        WorkTaskDto urgent = Task(priority: 1, due: Today.AddDays(20));
        WorkTaskDto minorSoonest = Task(priority: 60, due: Today.AddDays(1));
        WorkTaskDto middling = Task(priority: 30, due: null);
        WorkTaskDto minorSoon = Task(priority: 40, due: Today.AddDays(5));
        WorkTaskDto done = Task(priority: 1, due: Today, group: TaskStateGroup.Done);

        IEnumerable<WorkTaskDto> ordered = new[] { urgent, minorSoonest, middling, minorSoon, done }.InListOrder();

        Assert.Equal([minorSoonest.Id, minorSoon.Id, urgent.Id, middling.Id, done.Id], ordered.Select(task => task.Id));
    }

    private static WorkTaskDto Task(int priority, DateOnly? due, TaskStateGroup group = TaskStateGroup.Ready) =>
        new(Guid.NewGuid(), $"Priority {priority}", Guid.NewGuid(), group.ToString(), group, priority, due, Guid.NewGuid(), Created, Created);
}
