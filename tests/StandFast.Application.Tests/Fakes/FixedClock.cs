using StandFast.Domain.Abstractions;

namespace StandFast.Application.Tests.Fakes;

/// <summary>Deterministic clock so timestamp assertions do not depend on when the suite runs.</summary>
public sealed class FixedClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public DateOnly Today(TimeZoneInfo timeZone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, timeZone).DateTime);
}
