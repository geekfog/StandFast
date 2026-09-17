using System.Globalization;
using Azure;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;
using StandFast.Infrastructure.Storage;
using StandFast.Infrastructure.TableEntities;

namespace StandFast.Infrastructure.Mapping;

/// <summary>The only conversion between domain entities and their Azure Table representations, including the enum/time/date encodings storage requires.</summary>
public static class TableEntityMappings
{
    private const int MinutesPerHour = 60;

    public static Person ToDomain(this PersonTableEntity entity) => new()
    {
        Id = Guid.Parse(entity.RowKey),
        FirstName = entity.FirstName,
        LastName = entity.LastName,
        Email = entity.Email,
        DisplayAs = entity.DisplayAs,
        Notes = entity.Notes,
        IsActive = entity.IsActive,
        CreatedUtc = entity.CreatedUtc,
        ModifiedUtc = entity.ModifiedUtc,
        ETag = entity.ETag.ToString(),
    };

    public static PersonTableEntity ToTableEntity(this Person person) => new()
    {
        PartitionKey = StorageKeys.PersonPartition,
        RowKey = StorageKeys.PersonRowKey(person.Id),
        FirstName = person.FirstName,
        LastName = person.LastName,
        Email = person.Email,
        DisplayAs = person.DisplayAs,
        Notes = person.Notes,
        IsActive = person.IsActive,
        CreatedUtc = person.CreatedUtc,
        ModifiedUtc = person.ModifiedUtc,
        ETag = ToETag(person.ETag),
    };

    public static Standup ToDomain(this StandupTableEntity entity) => new()
    {
        Id = Guid.Parse(entity.RowKey),
        Name = entity.Name,
        Description = entity.Description,
        RecurrenceDays = (MeetingDays)entity.RecurrenceDays,
        StartTimeLocal = new TimeOnly(entity.StartMinutesLocal / MinutesPerHour, entity.StartMinutesLocal % MinutesPerHour),
        TimeZoneId = entity.TimeZoneId,
        IsActive = entity.IsActive,
        CreatedUtc = entity.CreatedUtc,
        ModifiedUtc = entity.ModifiedUtc,
        ETag = entity.ETag.ToString(),
    };

    public static StandupTableEntity ToTableEntity(this Standup standup) => new()
    {
        PartitionKey = StorageKeys.StandupPartition,
        RowKey = StorageKeys.StandupRowKey(standup.Id),
        Name = standup.Name,
        Description = standup.Description,
        RecurrenceDays = (int)standup.RecurrenceDays,
        StartMinutesLocal = (standup.StartTimeLocal.Hour * MinutesPerHour) + standup.StartTimeLocal.Minute,
        TimeZoneId = standup.TimeZoneId,
        IsActive = standup.IsActive,
        CreatedUtc = standup.CreatedUtc,
        ModifiedUtc = standup.ModifiedUtc,
        ETag = ToETag(standup.ETag),
    };

    public static StandupMember ToDomain(this StandupMemberTableEntity entity) => new()
    {
        StandupId = Guid.Parse(entity.PartitionKey),
        PersonId = Guid.Parse(entity.RowKey),
        DisplayOrder = entity.DisplayOrder,
        IsActive = entity.IsActive,
        CreatedUtc = entity.CreatedUtc,
        ETag = entity.ETag.ToString(),
    };

    public static StandupMemberTableEntity ToTableEntity(this StandupMember member) => new()
    {
        PartitionKey = StorageKeys.MemberPartitionKey(member.StandupId),
        RowKey = StorageKeys.MemberRowKey(member.PersonId),
        DisplayOrder = member.DisplayOrder,
        IsActive = member.IsActive,
        CreatedUtc = member.CreatedUtc,
        ETag = ToETag(member.ETag),
    };

    public static StandupEntry ToDomain(this StandupEntryTableEntity entity) => new()
    {
        StandupId = entity.StandupId,
        PersonId = entity.PersonId,
        MeetingDate = DateOnly.ParseExact(entity.MeetingDateKey, MeetingCalendar.DateKeyFormat, CultureInfo.InvariantCulture),
        State = (AttendanceState)entity.State,
        MarkedAvailableUtc = entity.MarkedAvailableUtc,
        PresentedUtc = entity.PresentedUtc,
        Update = entity.Update,
        Blockers = entity.Blockers,
        UpdateSavedUtc = entity.UpdateSavedUtc,
        ETag = entity.ETag.ToString(),
    };

    public static StandupEntryTableEntity ToTableEntity(this StandupEntry entry) => new()
    {
        PartitionKey = StorageKeys.EntryPartitionKey(entry.StandupId, entry.PersonId),
        RowKey = StorageKeys.EntryRowKey(entry.MeetingDate),
        StandupId = entry.StandupId,
        PersonId = entry.PersonId,
        MeetingDateKey = MeetingCalendar.ToDateKey(entry.MeetingDate),
        State = (int)entry.State,
        MarkedAvailableUtc = entry.MarkedAvailableUtc,
        PresentedUtc = entry.PresentedUtc,
        Update = entry.Update,
        Blockers = entry.Blockers,
        UpdateSavedUtc = entry.UpdateSavedUtc,
        ETag = ToETag(entry.ETag),
    };

    private static ETag ToETag(string? value) => string.IsNullOrWhiteSpace(value) ? ETag.All : new ETag(value);
}
