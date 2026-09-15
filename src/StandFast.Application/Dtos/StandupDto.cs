using StandFast.Domain.Enums;

namespace StandFast.Application.Dtos;

public sealed record StandupDto(Guid Id, string Name, string? Description, MeetingDays RecurrenceDays, TimeOnly StartTimeLocal, string TimeZoneId, bool IsActive);
