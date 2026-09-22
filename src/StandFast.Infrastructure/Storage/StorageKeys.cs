using System.Globalization;
using StandFast.Domain.Common;

namespace StandFast.Infrastructure.Storage;

/// <summary>Builds every partition and row key used against Azure Table Storage. Key shape is a storage concern and lives only here.</summary>
public static class StorageKeys
{
    /// <summary>People and standups are small, directory-style collections, so each uses a single fixed partition and is listed with one partition scan.</summary>
    public const string PersonPartition = "Person";

    public const string StandupPartition = "Standup";

    /// <summary>Separator for composite keys. It is not one of the <see cref="ForbiddenKeyCharacters"/>, and underscore stays readable in Storage Explorer.</summary>
    public const string KeySeparator = "_";

    /// <summary>Per-user settings are read one user at a time, so they share a partition and are told apart by the row key.</summary>
    public const string UserPreferencesPartition = "UserPreferences";

    /// <summary>Characters Azure Table Storage rejects in a partition or row key. Control characters are rejected as well and are tested for directly.</summary>
    private static readonly char[] ForbiddenKeyCharacters = ['/', '\\', '#', '?'];

    /// <summary>Upper bound used to invert a <c>yyyyMMdd</c> date so row keys sort newest first. Value is 9999-12-31 rounded up to all nines.</summary>
    private const int InvertedDateCeiling = 99999999;

    private const string InvertedDateFormat = "D8";

    public static string PersonRowKey(Guid personId) => personId.ToString("D");

    /// <summary>
    /// Row key for one user's settings. The value is the identity provider's subject identifier, which arrives from outside the app, so the characters
    /// Azure Table keys reject are folded onto the separator. Providers issue alphanumeric subjects, making this a guard rather than a transformation
    /// that normally changes anything.
    /// </summary>
    public static string UserPreferencesRowKey(string userId) => string.Concat(userId.Select(character =>
        ForbiddenKeyCharacters.Contains(character) || char.IsControl(character) ? KeySeparator[0] : character));

    public static string StandupRowKey(Guid standupId) => standupId.ToString("D");

    public static string MemberPartitionKey(Guid standupId) => standupId.ToString("D");

    public static string MemberRowKey(Guid personId) => personId.ToString("D");

    /// <summary>Highest value the <c>D</c> format can produce, so a partition key built from it bounds every person within one standup.</summary>
    private static readonly Guid MaxGuid = new(Enumerable.Repeat((byte)0xFF, 16).ToArray());

    /// <summary>Entries partition by standup plus person, so one participant's whole history is a single partition.</summary>
    public static string EntryPartitionKey(Guid standupId, Guid personId) => string.Concat(standupId.ToString("D"), KeySeparator, personId.ToString("D"));

    /// <summary>
    /// Inclusive partition key bounds covering every participant of one standup. Person ids occupy the key's second half, so the all-zero and
    /// all-ones guids bracket them and a range query reads one standup's entries without scanning other standups.
    /// </summary>
    public static (string From, string To) EntryPartitionRange(Guid standupId) => (EntryPartitionKey(standupId, Guid.Empty), EntryPartitionKey(standupId, MaxGuid));

    /// <summary>
    /// Inclusive row key bounds for a date range. Row keys are inverted dates, so the later date produces the lower key and the bounds swap
    /// relative to the dates they came from.
    /// </summary>
    public static (string From, string To) EntryRowKeyRange(DateOnly from, DateOnly to) => (EntryRowKey(to), EntryRowKey(from));

    /// <summary>
    /// Row key is the meeting date inverted, so ascending row-key order is descending date order. A range query of
    /// <c>RowKey ge EntryRowKey(date)</c> therefore returns the requested date first and the most recent earlier date second,
    /// which is exactly the current-plus-prior pair the board needs, in one round trip.
    /// </summary>
    public static string EntryRowKey(DateOnly meetingDate)
    {
        int dateNumber = int.Parse(MeetingCalendar.ToDateKey(meetingDate), CultureInfo.InvariantCulture);
        return (InvertedDateCeiling - dateNumber).ToString(InvertedDateFormat, CultureInfo.InvariantCulture);
    }

    public static DateOnly MeetingDateFromRowKey(string rowKey)
    {
        int inverted = int.Parse(rowKey, CultureInfo.InvariantCulture);
        return DateOnly.ParseExact((InvertedDateCeiling - inverted).ToString(InvertedDateFormat, CultureInfo.InvariantCulture), MeetingCalendar.DateKeyFormat, CultureInfo.InvariantCulture);
    }
}
