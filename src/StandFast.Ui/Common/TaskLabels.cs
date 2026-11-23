using MudBlazor;
using StandFast.Domain.Enums;

namespace StandFast.Ui.Common;

/// <summary>Wording, icons, and colors for tasks and task states, so the Organization screen and the board's task list agree.</summary>
public static class TaskLabels
{
    public const string Tasks = "Tasks";
    public const string TaskStates = "Task states";
    public const string State = "State";
    public const string Grouping = "State grouping";
    public const string Priority = "Priority";
    public const string Title = "Title";
    public const string Titles = "Titles";
    public const string TitlesHint = "One task per line. Each is added with the settings below.";
    public const string RequestedBy = "Requested by";
    public const string Created = "Created";

    /// <summary>Shown for a requester who has since been removed from People.</summary>
    public const string RequesterRemoved = "No longer in People";
    public const string Due = "Due";

    /// <summary>Heads the date column, and names the toggle, while done tasks are listed, which there show when they were done instead of their due date.</summary>
    public const string Done = "Done";

    public const string TasksIcon = Icons.Material.Filled.Checklist;
    public const string TaskStatesIcon = Icons.Material.Filled.Rule;
    public const string ShowDoneIcon = Icons.Material.Outlined.Visibility;
    public const string HideDoneIcon = Icons.Material.Filled.Visibility;

    public static string DateColumn(bool showDone) => showDone ? Done : Due;

    /// <summary>Hover text on a highlighted due date: the task is due before more urgent tasks that working by priority would reach first.</summary>
    public static string DueAheadOfMoreUrgent(int count) => $"Due before {count} more urgent {(count == 1 ? "task" : "tasks")}, which priority order puts first.";

    public static string PriorityHint => $"{Domain.Common.DomainLimits.TaskPriorityHighest} is the most urgent, {Domain.Common.DomainLimits.TaskPriorityLowest} the least.";

    public static string GroupName(TaskStateGroup group) => group switch
    {
        TaskStateGroup.InFlight => "In-Flight",
        _ => group.ToString(),
    };

    public static Color GroupColor(TaskStateGroup? group) => group switch
    {
        TaskStateGroup.Review => Color.Info,
        TaskStateGroup.Ready => Color.Primary,
        TaskStateGroup.InFlight => Color.Success,
        TaskStateGroup.Stopped => Color.Warning,
        _ => Color.Default,
    };
}
