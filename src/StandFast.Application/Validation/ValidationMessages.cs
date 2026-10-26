using FluentValidation;
using FluentValidation.Results;

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

    /// <summary>Raises one failure against one property, for a rule that can only be checked against what is already stored.</summary>
    public static ValidationException Failure(string propertyName, string message) => new([new ValidationFailure(propertyName, message)]);
}
