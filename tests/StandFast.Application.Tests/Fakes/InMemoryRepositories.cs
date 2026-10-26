using StandFast.Domain.Abstractions;
using StandFast.Domain.Common;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Application.Tests.Fakes;

/// <summary>In-memory stand-ins for the storage layer. Application logic is tested against the repository interfaces, with no Azure dependency.</summary>
public sealed class InMemoryPersonRepository : IPersonRepository
{
    private readonly Dictionary<Guid, Person> people = [];
    private readonly Dictionary<Guid, PersonPhoto> photos = [];
    private readonly List<PersonDayOff> daysOff = [];

    public Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Person>>([.. people.Values]);

    public Task<Person?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(people.GetValueOrDefault(id));

    public Task UpsertAsync(Person person, CancellationToken cancellationToken = default)
    {
        people[person.Id] = person;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        people.Remove(id);
        photos.Remove(id);
        daysOff.RemoveAll(dayOff => dayOff.PersonId == id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PersonDayOff>> GetDaysOffAsync(Guid personId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PersonDayOff>>([.. daysOff.Where(dayOff => dayOff.PersonId == personId)]);

    public Task<IReadOnlyList<PersonDayOff>> GetDaysOffAsync(DateRange dates, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PersonDayOff>>([.. daysOff.Where(dayOff => dayOff.Dates.Overlaps(dates))]);

    public Task UpsertDayOffAsync(PersonDayOff dayOff, CancellationToken cancellationToken = default)
    {
        daysOff.RemoveAll(other => other.Id == dayOff.Id);
        daysOff.Add(dayOff);
        return Task.CompletedTask;
    }

    public Task DeleteDayOffAsync(Guid personId, Guid dayOffId, CancellationToken cancellationToken = default)
    {
        daysOff.RemoveAll(dayOff => dayOff.PersonId == personId && dayOff.Id == dayOffId);
        return Task.CompletedTask;
    }

    public Task<PersonPhoto?> GetPhotoAsync(Guid personId, CancellationToken cancellationToken = default) => Task.FromResult(photos.GetValueOrDefault(personId));

    public Task UpsertPhotoAsync(PersonPhoto photo, CancellationToken cancellationToken = default)
    {
        photos[photo.PersonId] = photo;
        return Task.CompletedTask;
    }

    public Task DeletePhotoAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        photos.Remove(personId);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryStandupRepository : IStandupRepository
{
    private readonly Dictionary<Guid, Standup> standups = [];
    private readonly Dictionary<(Guid StandupId, Guid PersonId, RosterRole Role), StandupMember> members = [];
    private readonly Dictionary<(Guid StandupId, DateOnly MeetingDate), StandupMeeting> meetings = [];
    private readonly List<Sprint> sprints = [];

    public Task<IReadOnlyList<Standup>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Standup>>([.. standups.Values]);

    public Task<Standup?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(standups.GetValueOrDefault(id));

    public Task UpsertAsync(Standup standup, CancellationToken cancellationToken = default)
    {
        standups[standup.Id] = standup;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        standups.Remove(id);
        foreach ((Guid StandupId, Guid PersonId, RosterRole Role) key in members.Keys.Where(key => key.StandupId == id).ToList())
        {
            members.Remove(key);
        }

        foreach ((Guid StandupId, DateOnly MeetingDate) key in meetings.Keys.Where(key => key.StandupId == id).ToList())
        {
            meetings.Remove(key);
        }

        sprints.RemoveAll(sprint => sprint.StandupId == id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Sprint>> GetSprintsAsync(Guid standupId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Sprint>>([.. sprints.Where(sprint => sprint.StandupId == standupId)]);

    public Task UpsertSprintAsync(Sprint sprint, CancellationToken cancellationToken = default)
    {
        sprints.RemoveAll(other => other.Id == sprint.Id);
        sprints.Add(sprint);
        return Task.CompletedTask;
    }

    public Task DeleteSprintAsync(Guid standupId, Guid sprintId, CancellationToken cancellationToken = default)
    {
        sprints.RemoveAll(sprint => sprint.StandupId == standupId && sprint.Id == sprintId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StandupMember>> GetMembersAsync(Guid standupId, RosterRole role, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StandupMember>>([.. members.Values.Where(member => member.StandupId == standupId && member.Role == role)]);

    public Task<IReadOnlyList<StandupMember>> GetAllMembersAsync(RosterRole role, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StandupMember>>([.. members.Values.Where(member => member.Role == role)]);

    public Task UpsertMemberAsync(StandupMember member, CancellationToken cancellationToken = default)
    {
        members[(member.StandupId, member.PersonId, member.Role)] = member;
        return Task.CompletedTask;
    }

    public Task RemoveMemberAsync(Guid standupId, Guid personId, RosterRole role, CancellationToken cancellationToken = default)
    {
        members.Remove((standupId, personId, role));
        return Task.CompletedTask;
    }

    public Task<StandupMeeting?> GetMeetingAsync(Guid standupId, DateOnly meetingDate, CancellationToken cancellationToken = default) =>
        Task.FromResult(meetings.GetValueOrDefault((standupId, meetingDate)));

    public Task UpsertMeetingAsync(StandupMeeting meeting, CancellationToken cancellationToken = default)
    {
        meetings[(meeting.StandupId, meeting.MeetingDate)] = meeting;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StandupMeeting>> GetMeetingsAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StandupMeeting> found =
        [
            .. meetings.Values.Where(meeting => meeting.StandupId == standupId && meeting.MeetingDate >= from && meeting.MeetingDate <= to),
        ];

        return Task.FromResult(found);
    }
}

/// <summary>Mirrors the real repository's contract: the prior entry is the most recent one strictly before the requested date.</summary>
public sealed class InMemoryStandupEntryRepository : IStandupEntryRepository
{
    private readonly Dictionary<(Guid StandupId, Guid PersonId, DateOnly MeetingDate), StandupEntry> entries = [];

    public Task<StandupEntryPair> GetCurrentAndPriorAsync(Guid standupId, Guid personId, DateOnly meetingDate, CancellationToken cancellationToken = default)
    {
        StandupEntry? current = entries.GetValueOrDefault((standupId, personId, meetingDate));
        StandupEntry? prior = entries.Values
            .Where(entry => entry.StandupId == standupId && entry.PersonId == personId && entry.MeetingDate < meetingDate)
            .MaxBy(entry => entry.MeetingDate);

        return Task.FromResult(new StandupEntryPair(current, prior));
    }

    public Task UpsertAsync(StandupEntry entry, CancellationToken cancellationToken = default)
    {
        entries[(entry.StandupId, entry.PersonId, entry.MeetingDate)] = entry;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid standupId, Guid personId, DateOnly meetingDate, CancellationToken cancellationToken = default)
    {
        entries.Remove((standupId, personId, meetingDate));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StandupEntry>> GetOnDateAsync(Guid standupId, DateOnly meetingDate, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StandupEntry>>([.. entries.Values.Where(entry => entry.StandupId == standupId && entry.MeetingDate == meetingDate)]);

    public Task<IReadOnlyCollection<DateOnly>> GetPresentedDatesAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<DateOnly> dates = entries.Values
            .Where(entry => entry.StandupId == standupId && entry.State == AttendanceState.Presented && entry.MeetingDate >= from && entry.MeetingDate <= to)
            .Select(entry => entry.MeetingDate)
            .ToHashSet();

        return Task.FromResult(dates);
    }

    public Task<IReadOnlyList<Presentation>> GetPresentationsAsync(Guid standupId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Presentation> presentations =
        [
            .. entries.Values
                .Where(entry => entry.StandupId == standupId && entry.MeetingDate >= from && entry.MeetingDate <= to)
                .Select(entry => entry.CompletedTurn)
                .OfType<Presentation>(),
        ];

        return Task.FromResult(presentations);
    }
}

public sealed class InMemoryHolidayRepository : IHolidayRepository
{
    private readonly Dictionary<Guid, Holiday> holidays = [];

    public Task<IReadOnlyList<Holiday>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Holiday>>([.. holidays.Values]);

    public Task<Holiday?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(holidays.GetValueOrDefault(id));

    public Task UpsertAsync(Holiday holiday, CancellationToken cancellationToken = default)
    {
        holidays[holiday.Id] = holiday;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        holidays.Remove(id);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryUserPreferencesRepository : IUserPreferencesRepository
{
    private readonly Dictionary<string, UserPreferences> preferences = [];

    public Task<UserPreferences?> GetAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(preferences.GetValueOrDefault(userId));

    public Task UpsertAsync(UserPreferences settings, CancellationToken cancellationToken = default)
    {
        preferences[settings.UserId] = settings;
        return Task.CompletedTask;
    }
}
