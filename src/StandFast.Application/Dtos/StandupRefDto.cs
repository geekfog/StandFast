namespace StandFast.Application.Dtos;

/// <summary>A standup as named on another screen's row: its id for matching, and its name for display, since names are not unique.</summary>
public sealed record StandupRefDto(Guid Id, string Name);
