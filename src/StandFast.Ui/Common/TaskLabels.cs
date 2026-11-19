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

    /// <summary>Characters of a task's title the board's compact task list shows, so the row stays one line; the full title shows on hover and in the edit dialog.</summary>
    public const int ShownTitleLength = 20;

    private const string Ellipsis = "…";

    public static string ShortTitle(string title) => title.Length <= ShownTitleLength ? title : string.Concat(title.AsSpan(0, ShownTitleLength).TrimEnd(), Ellipsis);

    public static string DateColumn(bool showDone) => showDone ? Done : Due;

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
