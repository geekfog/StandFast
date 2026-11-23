namespace StandFast.Application.Notifications;

/// <summary>A change saved to one standup's board for one date. <see cref="OriginId"/> names the circuit that saved it, which already shows the change.</summary>
public sealed record BoardChange(Guid StandupId, DateOnly MeetingDate, Guid OriginId);
