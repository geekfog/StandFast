namespace StandFast.Application.Services;

/// <summary>Thrown when someone scheduled to be off on a date is marked present on it. The message is written to be shown as it stands.</summary>
public sealed class PersonOffException(string displayName, string? note)
    : InvalidOperationException($"{displayName} is scheduled to be off on this date{(note is null ? string.Empty : $" ({note})")}, so they cannot be marked present.");
