namespace StandFast.Domain.Abstractions;

/// <summary>Injectable time source. Nothing outside an <see cref="IClock"/> implementation reads the system clock, so "today" is controllable in tests.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>The current date in the supplied time zone. The board opens on this date.</summary>
    DateOnly Today(TimeZoneInfo timeZone);
}
