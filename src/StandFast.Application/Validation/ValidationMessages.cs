using FluentValidation;
using FluentValidation.Results;
using StandFast.Domain.Common;

namespace StandFast.Application.Validation;

/// <summary>Validation messages shared by more than one validator.</summary>
public static class ValidationMessages
{
    public const string UnknownTimeZone = "Select a time zone from the list.";

    public const string PhotoTooLarge = "This photo is too large to save. Try a smaller or simpler image.";

    public const string SupervisorNotFound = "That supervisor is no longer in People. Select another.";

    /// <summary>Names the people in a supervisor loop in reporting order, the first repeated at the end, so the reader can see which link to break.</summary>
    public static string SupervisorLoop(IEnumerable<string> namesInLoop) => $"Supervisors cannot loop: {string.Join(" → ", namesInLoop)}.";

    public const string DateRequired = "Pick a date.";

    public const string DatesRequired = "Pick a start and an end date.";

    public const string EndBeforeStart = "The end date cannot be before the start date.";

    public static string HolidayDateTaken(string existingName) => $"{existingName} is already recorded on that date.";

    public static string SprintOverlap(string existingName) => $"Sprints cannot overlap, and these dates overlap {existingName}.";

    public static string TaskStateTitleTaken(string existingTitle) => $"{existingTitle} is already a task state.";

    public static string TaskStateInUse(string title) => $"{title} cannot be removed while a task is in it. Move those tasks to another state first.";

    public const string TaskStateRequired = "Pick a state.";

    public static readonly string TaskTitleTooLong = $"Each title can be at most {DomainLimits.TaskTitleMaxLength} characters.";

    public const string TaskTitleSingle = "A task has one title. Add a new task for each further title.";

    public const string TaskRequesterRequired = "Pick who requested it.";

    public static readonly string TaskPriorityRange = $"Enter a priority from {DomainLimits.TaskPriorityHighest}, the most urgent, to {DomainLimits.TaskPriorityLowest}.";

    public const string TaskStateNotFound = "That state is no longer defined. Pick another.";

    public const string TaskRequesterNotFound = "That person is no longer in People. Pick another.";

    public const string TaskOwnerNotFound = "Your sign-in address does not match anyone in People, so you have no task list.";

    public const string TaskNotFound = "That task has been removed from your list.";

    /// <summary>Raises one failure against one property, for a rule that can only be checked against what is already stored.</summary>
    public static ValidationException Failure(string propertyName, string message) => new([new ValidationFailure(propertyName, message)]);
}
