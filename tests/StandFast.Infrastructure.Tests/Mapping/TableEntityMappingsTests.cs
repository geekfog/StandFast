using StandFast.Domain.Entities;
using StandFast.Domain.Enums;
using StandFast.Infrastructure.Mapping;
using StandFast.Infrastructure.TableEntities;

namespace StandFast.Infrastructure.Tests.Mapping;

/// <summary>Azure Tables has no enum, date-only, or time-only type, so the encodings those properties use are round-trip tested.</summary>
public sealed class TableEntityMappingsTests
{
    [Fact]
    public void Standup_RoundTripsScheduleAndStartTime()
    {
        Standup standup = new()
        {
            Name = "Platform daily",
            Description = "Core services",
            RecurrenceDays = MeetingDays.Weekdays,
            StartTimeLocal = new TimeOnly(9, 45),
            TimeZoneId = "UTC",
        };

        Standup restored = standup.ToTableEntity().ToDomain();

        Assert.Equal(standup.Id, restored.Id);
        Assert.Equal(standup.RecurrenceDays, restored.RecurrenceDays);
        Assert.Equal(standup.StartTimeLocal, restored.StartTimeLocal);
        Assert.Equal(standup.TimeZoneId, restored.TimeZoneId);
    }

    [Fact]
    public void Person_RoundTripsIdentityAndContactDetails()
    {
        Person person = new() { FirstName = "Ada", LastName = "Lovelace", Email = "ada.lovelace@example.com" };

        Person restored = person.ToTableEntity().ToDomain();

        Assert.Equal(person.Id, restored.Id);
        Assert.Equal("Ada Lovelace", restored.DisplayName);
        Assert.Equal("AL", restored.Initials);
    }

    [Fact]
    public void StandupEntry_RoundTripsAttendanceAndMarkdown()
    {
        StandupEntry entry = new()
        {
            StandupId = Guid.CreateVersion7(),
            PersonId = Guid.CreateVersion7(),
            MeetingDate = new DateOnly(2026, 9, 15),
            State = AttendanceState.Presented,
            Update = "- Shipped **the** thing",
            Blockers = "Waiting on review",
            PresentedUtc = DateTimeOffset.UnixEpoch,
        };

        StandupEntryTableEntity stored = entry.ToTableEntity();
        StandupEntry restored = stored.ToDomain();

        Assert.Equal("20260915", stored.MeetingDateKey);
        Assert.Equal(entry.MeetingDate, restored.MeetingDate);
        Assert.Equal(AttendanceState.Presented, restored.State);
        Assert.Equal(entry.Update, restored.Update);
        Assert.Equal(entry.Blockers, restored.Blockers);
    }

    [Fact]
    public void StandupMember_RoundTripsCompositeKey()
    {
        StandupMember member = new() { StandupId = Guid.CreateVersion7(), PersonId = Guid.CreateVersion7(), DisplayOrder = 20 };

        StandupMember restored = member.ToTableEntity().ToDomain();

        Assert.Equal(member.StandupId, restored.StandupId);
        Assert.Equal(member.PersonId, restored.PersonId);
        Assert.Equal(member.DisplayOrder, restored.DisplayOrder);
    }
}
