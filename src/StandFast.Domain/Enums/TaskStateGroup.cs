namespace StandFast.Domain.Enums;

/// <summary>The fixed stage a customizable task state belongs to. Tasks whose state is in <see cref="Done"/> are done.</summary>
public enum TaskStateGroup
{
    Review = 0,
    Ready = 1,
    InFlight = 2,
    Stopped = 3,
    Done = 4,
}

public static class TaskStateGroupExtensions
{
    /// <summary>Groups in the order screens list them, which is the order work moves through them.</summary>
    public static IReadOnlyList<TaskStateGroup> InDisplayOrder { get; } = [TaskStateGroup.Review, TaskStateGroup.Ready, TaskStateGroup.InFlight, TaskStateGroup.Stopped, TaskStateGroup.Done];

    /// <summary>The group's position in <see cref="InDisplayOrder"/>, for sorting.</summary>
    public static int DisplayRank(this TaskStateGroup group) => InDisplayOrder.ToList().IndexOf(group);

    public static bool IsDone(this TaskStateGroup group) => group == TaskStateGroup.Done;
}
