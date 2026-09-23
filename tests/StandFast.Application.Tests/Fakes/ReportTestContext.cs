using StandFast.Application.Services;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Fakes;

/// <summary>Builds a <see cref="ReportService"/> over in-memory storage with a fixed "today", so each test states only the turns that matter to it.</summary>
public sealed class ReportTestContext
{
    public static readonly DateOnly Today = new(2026, 9, 15);
    public static readonly DateTimeOffset NineAm = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);

    public ReportTestContext() => Service = new ReportService(Standups, People, Entries, new FixedClock(NineAm));

    public InMemoryPersonRepository People { get; } = new();

    public InMemoryStandupRepository Standups { get; } = new();

    public InMemoryStandupEntryRepository Entries { get; } = new();

    public ReportService Service { get; }

    public Standup Standup { get; } = new() { Name = "Platform daily" };

    public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

    public async Task<Person> AddPersonAsync(string firstName, string lastName)
    {
        await Standups.UpsertAsync(Standup);

        Person person = new() { FirstName = firstName, LastName = lastName, Email = $"{firstName}.{lastName}@example.com".ToLowerInvariant() };
        await People.UpsertAsync(person);

        return person;
    }

    /// <summary>Records a completed turn. <paramref name="minutesIntoMeeting"/> is what puts the turns of one meeting in order.</summary>
    public Task PresentedAsync(Person person, DateOnly meetingDate, int minutesIntoMeeting) => Entries.UpsertAsync(new StandupEntry
    {
        StandupId = Standup.Id,
        PersonId = person.Id,
        MeetingDate = meetingDate,
        State = AttendanceState.Presented,
        PresentedUtc = NineAm.AddDays(meetingDate.DayNumber - Today.DayNumber).AddMinutes(minutesIntoMeeting),
    });

    /// <summary>Closes a date at a given moment, which is what the activity report measures a standup's length against.</summary>
    public Task LockedAsync(DateOnly meetingDate, DateTimeOffset lockedUtc) => UpsertMeetingAsync(meetingDate, meeting => meeting.LockedUtc = lockedUtc);

    /// <summary>Records who ran a date, which annotates the date without making the standup have run.</summary>
    public Task LedByAsync(DateOnly meetingDate, Person leader) => UpsertMeetingAsync(meetingDate, meeting => meeting.LeaderPersonId = leader.Id);

    /// <summary>Records attendance without a turn, which is what someone present but never called on looks like.</summary>
    public Task AttendedWithoutPresentingAsync(Person person, DateOnly meetingDate) => Entries.UpsertAsync(new StandupEntry
    {
        StandupId = Standup.Id,
        PersonId = person.Id,
        MeetingDate = meetingDate,
        State = AttendanceState.Available,
        MarkedAvailableUtc = NineAm,
    });

    private Task UpsertMeetingAsync(DateOnly meetingDate, Action<StandupMeeting> change)
    {
        StandupMeeting meeting = new() { StandupId = Standup.Id, MeetingDate = meetingDate, CreatedUtc = NineAm };
        change(meeting);

        return Standups.UpsertMeetingAsync(meeting);
    }
}
