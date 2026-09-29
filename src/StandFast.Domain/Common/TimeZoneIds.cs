namespace StandFast.Domain.Common;

/// <summary>
/// Time zone ids are stored in IANA form (for example <c>America/Chicago</c>), which resolves on every host: Linux natively, and Windows through ICU.
/// A Windows id supplied by the host is converted on the way in, so the stored value does not depend on the machine that wrote it.
/// </summary>
public static class TimeZoneIds
{
    /// <summary>The host's own time zone in stored form, used as the default for anything scheduled in local time.</summary>
    public static string Local => ToStored(TimeZoneInfo.Local.Id);

    /// <summary>The IANA form of <paramref name="id"/> when it is a Windows id with a known mapping; otherwise <paramref name="id"/> unchanged.</summary>
    public static string ToStored(string id) => TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out string? ianaId) ? ianaId : id;

    /// <summary>The stored form of <paramref name="id"/>, or null when it is blank.</summary>
    public static string? ToStoredOrNull(string? id) => string.IsNullOrWhiteSpace(id) ? null : ToStored(id.Trim());

    /// <summary>True when <paramref name="id"/> names a time zone this host can resolve.</summary>
    public static bool IsKnown(string? id) => !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);

    /// <summary>The host's display name for <paramref name="id"/>, or the id itself when the host cannot resolve it.</summary>
    public static string DisplayName(string id) => TimeZoneInfo.TryFindSystemTimeZoneById(id, out TimeZoneInfo? zone) ? zone.DisplayName : id;

    /// <summary>The time zone <paramref name="id"/> names, or null when it is blank or the host cannot resolve it.</summary>
    public static TimeZoneInfo? Resolve(string? id) => !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out TimeZoneInfo? zone) ? zone : null;

    /// <summary>
    /// The instant a wall-clock <paramref name="time"/> on <paramref name="date"/> occurs in <paramref name="zone"/>. A time that falls in a daylight saving gap
    /// is taken as the end of the gap, the first valid time after it.
    /// </summary>
    public static DateTimeOffset ToInstant(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        DateTime local = date.ToDateTime(time, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(MinutesPerDaylightSavingStep);
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    /// <summary>How far <paramref name="zone"/>'s clock is ahead of <paramref name="reference"/>'s at <paramref name="instant"/>; negative when it is behind.</summary>
    public static TimeSpan OffsetFrom(TimeZoneInfo zone, TimeZoneInfo reference, DateTimeOffset instant) => zone.GetUtcOffset(instant) - reference.GetUtcOffset(instant);

    /// <summary>Granularity used to step out of a daylight saving gap. Every zone's clock changes on a whole multiple of this.</summary>
    private const int MinutesPerDaylightSavingStep = 15;
}
