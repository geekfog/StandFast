namespace StandFast.Application.Validation;

/// <summary>Validation messages shared by more than one validator.</summary>
public static class ValidationMessages
{
    public const string UnknownTimeZone = "Select a time zone from the list.";

    public const string PhotoTooLarge = "This photo is too large to save. Try a smaller or simpler image.";

    public const string SupervisorNotFound = "That supervisor is no longer in People. Select another.";

    /// <summary>Names the people in a supervisor loop in reporting order, the first repeated at the end, so the reader can see which link to break.</summary>
    public static string SupervisorLoop(IEnumerable<string> namesInLoop) => $"Supervisors cannot loop: {string.Join(" → ", namesInLoop)}.";
}
