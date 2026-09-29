using StandFast.Domain.Common;
using StandFast.Domain.Enums;

namespace StandFast.Application.Dtos;

public sealed class StandupEditDto
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public MeetingDays RecurrenceDays { get; set; } = MeetingDays.Weekdays;

    public TimeOnly StartTimeLocal { get; set; } = new(9, 0);

    public string TimeZoneId { get; set; } = TimeZoneIds.Local;

    public bool IsActive { get; set; } = true;
}
