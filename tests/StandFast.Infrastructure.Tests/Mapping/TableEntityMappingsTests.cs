using StandFast.Domain.Entities;
using StandFast.Domain.Enums;
using StandFast.Infrastructure.Mapping;
using StandFast.Infrastructure.TableEntities;

namespace StandFast.Infrastructure.Tests.Mapping;

/// <summary>Azure Tables has no enum, date-only, or time-only type, so the encodings those properties use are round-trip tested.</summary>
public sealed class TableEntityMappingsTests
{
    [Fact]
    public void Standup_RoundTripsScheduleStartTimeAndWhoCanPresent()
    {
        Standup standup = new()
        {
            Name = "Platform daily",
            Description = "Core services",
            RecurrenceDays = MeetingDays.Weekdays,
            StartTimeLocal = new TimeOnly(9, 45),
            TimeZoneId = "UTC",
            LeadersCanPresent = true,
        };

        Standup restored = standup.ToTableEntity().ToDomain();

        Assert.Equal(standup.Id, restored.Id);
        Assert.Equal(standup.RecurrenceDays, restored.RecurrenceDays);
        Assert.Equal(standup.StartTimeLocal, restored.StartTimeLocal);
        Assert.Equal(standup.TimeZoneId, restored.TimeZoneId);
        Assert.True(restored.LeadersCanPresent);
        Assert.False(restored.GuestsCanPresent);
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
    public void Person_RoundTripsTheDisplayOverrideAndNotes()
    {
        Person person = new() { FirstName = "Augusta", LastName = "King", Email = "ada@example.com", DisplayAs = "Ada Lovelace", Notes = "- Works Tuesdays" };

        Person restored = person.ToTableEntity().ToDomain();

        Assert.Equal("Ada Lovelace", restored.DisplayAs);
        Assert.Equal("Ada Lovelace", restored.DisplayName);
        Assert.Equal("Augusta King", restored.FullName);
        Assert.Equal("- Works Tuesdays", restored.Notes);
    }

    [Fact]
    public void Person_RoundTripsTheProfileAndLocation()
    {
        Person person = new() { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com", Title = "Analyst", Organization = "Engines", Department = "Analysis", PhotoSavedUtc = new DateTimeOffset(2026, 9, 29, 15, 0, 0, TimeSpan.Zero), City = "Austin", StateOrRegion = "TX", TimeZoneId = "America/Chicago", StartDate = new DateOnly(2021, 3, 1), SupervisorId = Guid.CreateVersion7() };

        Person restored = person.ToTableEntity().ToDomain();

        Assert.Equal("Analyst", restored.Title);
        Assert.Equal("Engines", restored.Organization);
        Assert.Equal("Analysis", restored.Department);
        Assert.Equal(person.PhotoSavedUtc, restored.PhotoSavedUtc);
        Assert.Equal("Austin", restored.City);
        Assert.Equal("TX", restored.StateOrRegion);
        Assert.Equal("America/Chicago", restored.TimeZoneId);
        Assert.Equal(person.StartDate, restored.StartDate);
        Assert.Equal(person.SupervisorId, restored.SupervisorId);
    }

    [Fact]
    public void Person_WithoutAStartDate_RoundTripsAsNull()
    {
        Person restored = new Person { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" }.ToTableEntity().ToDomain();

        Assert.Null(restored.StartDate);
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
            ParkingLot = "Discuss the rollout",
            PresentedUtc = DateTimeOffset.UnixEpoch,
        };

        StandupEntryTableEntity stored = entry.ToTableEntity();
        StandupEntry restored = stored.ToDomain();

        Assert.Equal("20260915", stored.MeetingDateKey);
        Assert.Equal(entry.MeetingDate, restored.MeetingDate);
        Assert.Equal(AttendanceState.Presented, restored.State);
        Assert.Equal(entry.Update, restored.Update);
        Assert.Equal(entry.Blockers, restored.Blockers);
        Assert.Equal(entry.ParkingLot, restored.ParkingLot);
    }

    [Theory]
    [InlineData(RosterRole.Presenter)]
    [InlineData(RosterRole.Leader)]
    [InlineData(RosterRole.Guest)]
    public void StandupMember_RoundTripsCompositeKeyAndTakesItsRoleFromTheTableItCameFrom(RosterRole role)
    {
        StandupMember member = new() { StandupId = Guid.CreateVersion7(), PersonId = Guid.CreateVersion7(), Role = role, DisplayOrder = 20 };

        StandupMember restored = member.ToTableEntity().ToDomain(role);

        Assert.Equal(member.StandupId, restored.StandupId);
        Assert.Equal(member.PersonId, restored.PersonId);
        Assert.Equal(role, restored.Role);
        Assert.Equal(member.DisplayOrder, restored.DisplayOrder);
    }

    [Fact]
    public void StandupMeeting_RoundTripsTheDateTheLeaderWhoCanLeadAndTheLock()
    {
        StandupMeeting meeting = new()
        {
            StandupId = Guid.CreateVersion7(),
            MeetingDate = new DateOnly(2026, 9, 15),
            LeaderPersonId = Guid.CreateVersion7(),
            AnyAttendeeCanLead = true,
            LockedUtc = DateTimeOffset.UnixEpoch.AddHours(9),
            CreatedUtc = DateTimeOffset.UnixEpoch,
        };

        StandupMeetingTableEntity stored = meeting.ToTableEntity();
        StandupMeeting restored = stored.ToDomain();

        Assert.Equal("20260915", stored.RowKey);
        Assert.Equal(meeting.StandupId, restored.StandupId);
        Assert.Equal(meeting.MeetingDate, restored.MeetingDate);
        Assert.Equal(meeting.LeaderPersonId, restored.LeaderPersonId);
        Assert.True(restored.AnyAttendeeCanLead);
        Assert.Equal(meeting.LockedUtc, restored.LockedUtc);
        Assert.True(restored.IsLocked);
    }

    [Fact]
    public void CalendarEntries_RoundTripTheirDatesAndDuration()
    {
        Holiday holiday = new() { Date = new DateOnly(2026, 12, 24), Name = "Christmas Eve", Duration = HolidayDuration.HalfDay };
        Sprint sprint = new() { StandupId = Guid.CreateVersion7(), Name = "Sprint 29", StartDate = new DateOnly(2026, 9, 14), EndDate = new DateOnly(2026, 9, 25), Note = "Release freeze Thursday" };
        PersonDayOff dayOff = new() { PersonId = Guid.CreateVersion7(), StartDate = new DateOnly(2026, 9, 21), EndDate = new DateOnly(2026, 9, 22), Note = "Vacation" };

        Holiday restoredHoliday = holiday.ToTableEntity().ToDomain();
        Sprint restoredSprint = sprint.ToTableEntity().ToDomain();
        PersonDayOff restoredDayOff = dayOff.ToTableEntity().ToDomain();

        Assert.Equal((holiday.Id, holiday.Date, holiday.Name, holiday.Duration), (restoredHoliday.Id, restoredHoliday.Date, restoredHoliday.Name, restoredHoliday.Duration));
        Assert.Equal((sprint.Id, sprint.StandupId, sprint.Name, sprint.Dates, sprint.Note), (restoredSprint.Id, restoredSprint.StandupId, restoredSprint.Name, restoredSprint.Dates, restoredSprint.Note));
        Assert.Equal((dayOff.Id, dayOff.PersonId, dayOff.Dates, dayOff.Note), (restoredDayOff.Id, restoredDayOff.PersonId, restoredDayOff.Dates, restoredDayOff.Note));
    }

    [Fact]
    public void StandupMeeting_RoundTripsWithNobodyLeadingAndTheDateOpen()
    {
        StandupMeeting meeting = new() { StandupId = Guid.CreateVersion7(), MeetingDate = new DateOnly(2026, 9, 15), CreatedUtc = DateTimeOffset.UnixEpoch };

        StandupMeeting restored = meeting.ToTableEntity().ToDomain();

        Assert.Null(restored.LeaderPersonId);
        Assert.False(restored.AnyAttendeeCanLead);
        Assert.False(restored.IsLocked);
    }
}
