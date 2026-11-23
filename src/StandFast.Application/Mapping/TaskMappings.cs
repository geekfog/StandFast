using StandFast.Application.Dtos;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Mapping;

/// <summary>The only place task states and tasks are converted to and from their DTOs and put in list order.</summary>
public static class TaskMappings
{
    public static TaskStateDto ToDto(this TaskState state) => new(state.Id, state.Title, state.Group);

    public static TaskStateEditDto ToEditDto(this TaskStateDto state) => new() { Id = state.Id, Title = state.Title, Group = state.Group };

    public static void ApplyTo(this TaskStateEditDto dto, TaskState state)
    {
        state.Title = dto.Title.Trim();
        state.Group = dto.Group;
    }

    /// <summary>States in the order a picker lists them: by group, in the order work moves through the groups, then by title.</summary>
    public static IEnumerable<TaskState> InListOrder(this IEnumerable<TaskState> states) =>
        states.OrderBy(state => state.Group.DisplayRank()).ThenBy(state => state.Title, StringComparer.CurrentCultureIgnoreCase);

    public static WorkTaskDto ToDto(this WorkTask task, TaskState? state) => new(
        task.Id, task.Title, task.StateId, state?.Title, state?.Group, task.Priority, task.DueDate, task.RequestedByPersonId, task.CreatedUtc, task.LastUpdatedUtc);

    public static WorkTaskEditDto ToEditDto(this WorkTaskDto task) => new()
    {
        Id = task.Id,
        Title = task.Title,
        StateId = task.StateId,
        Priority = task.Priority,
        DueDate = task.DueDate,
        RequestedByPersonId = task.RequestedByPersonId,
    };

    /// <summary>
    /// The titles typed, one per non-blank line, each trimmed. Adding creates one task per title; an existing task has exactly one. A single line is the
    /// usual case, so a title with no line breaks is one title.
    /// </summary>
    public static IReadOnlyList<string> Titles(this WorkTaskEditDto dto) =>
        [.. dto.Title.Split(TitleSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static readonly char[] TitleSeparators = ['\r', '\n'];

    /// <summary>Called only after validation, which guarantees the state, priority, and requester are set. The title is one of <see cref="Titles"/>.</summary>
    public static void ApplyTo(this WorkTaskEditDto dto, WorkTask task, string title)
    {
        task.Title = title;
        task.StateId = dto.StateId!.Value;
        task.Priority = dto.Priority!.Value;
        task.DueDate = dto.DueDate;
        task.RequestedByPersonId = dto.RequestedByPersonId!.Value;
    }

    /// <summary>
    /// Tasks in list order. Open tasks come first, led by those <see cref="DueAheadOfMoreUrgent"/> flags, soonest due first and then most urgent. The other
    /// open tasks follow, most urgent priority first, then latest due date first with undated tasks after dated ones. Done tasks come last, most recently
    /// updated first. Ties go by title.
    /// </summary>
    public static IEnumerable<WorkTaskDto> InListOrder(this IEnumerable<WorkTaskDto> tasks)
    {
        List<WorkTaskDto> all = [.. tasks];
        IReadOnlyDictionary<Guid, int> dueAheadOfMoreUrgent = all.DueAheadOfMoreUrgent();

        return
        [
            .. all.Where(task => dueAheadOfMoreUrgent.ContainsKey(task.Id))
                .OrderBy(task => task.DueDate)
                .ThenBy(task => task.Priority)
                .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase),
            .. all.Where(task => !task.IsDone && !dueAheadOfMoreUrgent.ContainsKey(task.Id))
                .OrderBy(task => task.Priority)
                .ThenBy(task => task.DueDate is null)
                .ThenByDescending(task => task.DueDate)
                .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase),
            .. all.Where(task => task.IsDone)
                .OrderByDescending(task => task.LastUpdatedUtc)
                .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    /// <summary>
    /// Each open, dated task due before at least one more urgent open task, with how many, so working strictly by priority would reach it after its deadline
    /// is already behind the others'. These are the tasks whose place differs between priority order and due-date order. Undated tasks have no deadline to
    /// miss and are never counted on either side; equal priorities are not a conflict.
    /// </summary>
    public static IReadOnlyDictionary<Guid, int> DueAheadOfMoreUrgent(this IEnumerable<WorkTaskDto> tasks)
    {
        WorkTaskDto[] dated = [.. tasks.Where(task => !task.IsDone && task.DueDate is not null)];

        return dated
            .Select(task => (task.Id, Count: dated.Count(other => other.Priority < task.Priority && other.DueDate > task.DueDate)))
            .Where(conflict => conflict.Count > 0)
            .ToDictionary(conflict => conflict.Id, conflict => conflict.Count);
    }
}
