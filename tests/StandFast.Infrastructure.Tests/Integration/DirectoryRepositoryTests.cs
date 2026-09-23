using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Infrastructure.Tests.Integration;

/// <summary>Round-trips people, standups and rosters through Azurite, including the roster cleanup that follows deleting a standup.</summary>
public sealed class DirectoryRepositoryTests : IClassFixture<AzuriteTableFixture>
{
    private readonly AzuriteTableFixture fixture;

    public DirectoryRepositoryTests(AzuriteTableFixture fixture) => this.fixture = fixture;

    [AzuriteFact]
    public async Task People_RoundTripThroughStorage()
    {
        Person person = new() { FirstName = "Ada", LastName = "Lovelace", Email = "ada.lovelace@example.com", CreatedUtc = DateTimeOffset.UtcNow };

        await fixture.People.UpsertAsync(person);
        Person? stored = await fixture.People.GetAsync(person.Id);

        Assert.Equal("Ada Lovelace", stored?.DisplayName);
        Assert.Equal(person.Email, stored?.Email);
        Assert.True(stored?.IsActive);

        await fixture.People.DeleteAsync(person.Id);
        Assert.Null(await fixture.People.GetAsync(person.Id));
    }

    [AzuriteFact]
    public async Task Standups_RoundTripScheduleAndStartTime()
    {
        Standup standup = new()
        {
            Name = "Platform daily",
            RecurrenceDays = MeetingDays.Weekdays,
            StartTimeLocal = new TimeOnly(9, 45),
            TimeZoneId = "UTC",
            CreatedUtc = DateTimeOffset.UtcNow,
        };

        await fixture.Standups.UpsertAsync(standup);
        Standup? stored = await fixture.Standups.GetAsync(standup.Id);

        Assert.Equal(MeetingDays.Weekdays, stored?.RecurrenceDays);
        Assert.Equal(new TimeOnly(9, 45), stored?.StartTimeLocal);
        Assert.True(stored?.OccursOn(new DateOnly(2026, 9, 15)));
        Assert.False(stored?.OccursOn(new DateOnly(2026, 9, 19).AddDays(1)));
    }

    [AzuriteFact]
    public async Task DeletingAStandup_AlsoRemovesEveryRosterAndItsMeetings()
    {
        DateOnly meetingDate = new(2026, 9, 15);
        Standup standup = new() { Name = "Temporary", CreatedUtc = DateTimeOffset.UtcNow };
        await fixture.Standups.UpsertAsync(standup);

        foreach (int order in new[] { 10, 20 })
        {
            await fixture.Standups.UpsertMemberAsync(new StandupMember { StandupId = standup.Id, PersonId = Guid.CreateVersion7(), DisplayOrder = order });
        }

        Guid leaderId = Guid.CreateVersion7();
        await fixture.Standups.UpsertMemberAsync(new StandupMember { StandupId = standup.Id, PersonId = leaderId, Role = RosterRole.Leader, DisplayOrder = 10 });
        await fixture.Standups.UpsertMeetingAsync(new StandupMeeting { StandupId = standup.Id, MeetingDate = meetingDate, LeaderPersonId = leaderId, CreatedUtc = DateTimeOffset.UtcNow });

        Assert.Equal(2, (await fixture.Standups.GetMembersAsync(standup.Id, RosterRole.Presenter)).Count);
        Assert.Single(await fixture.Standups.GetMembersAsync(standup.Id, RosterRole.Leader));

        await fixture.Standups.DeleteAsync(standup.Id);

        Assert.Null(await fixture.Standups.GetAsync(standup.Id));
        Assert.Empty(await fixture.Standups.GetMembersAsync(standup.Id, RosterRole.Presenter));
        Assert.Empty(await fixture.Standups.GetMembersAsync(standup.Id, RosterRole.Leader));
        Assert.Null(await fixture.Standups.GetMeetingAsync(standup.Id, meetingDate));
    }

    [AzuriteFact]
    public async Task EachRoleKeepsItsOwnMembershipForTheSamePerson()
    {
        Standup standup = new() { Name = "Two hats", CreatedUtc = DateTimeOffset.UtcNow };
        Guid personId = Guid.CreateVersion7();

        await fixture.Standups.UpsertAsync(standup);
        await fixture.Standups.UpsertMemberAsync(new StandupMember { StandupId = standup.Id, PersonId = personId, Role = RosterRole.Presenter, DisplayOrder = 10 });
        await fixture.Standups.UpsertMemberAsync(new StandupMember { StandupId = standup.Id, PersonId = personId, Role = RosterRole.Leader, DisplayOrder = 30 });

        await fixture.Standups.RemoveMemberAsync(standup.Id, personId, RosterRole.Presenter);

        Assert.Empty(await fixture.Standups.GetMembersAsync(standup.Id, RosterRole.Presenter));

        StandupMember leader = Assert.Single(await fixture.Standups.GetMembersAsync(standup.Id, RosterRole.Leader));

        Assert.Equal(RosterRole.Leader, leader.Role);
        Assert.Equal(30, leader.DisplayOrder);
    }

    [AzuriteFact]
    public async Task AMeetingRoundTripsItsLeaderForOneDateOnly()
    {
        DateOnly meetingDate = new(2026, 9, 15);
        Standup standup = new() { Name = "Led", CreatedUtc = DateTimeOffset.UtcNow };
        Guid leaderId = Guid.CreateVersion7();

        await fixture.Standups.UpsertAsync(standup);
        await fixture.Standups.UpsertMeetingAsync(new StandupMeeting { StandupId = standup.Id, MeetingDate = meetingDate, LeaderPersonId = leaderId, CreatedUtc = DateTimeOffset.UtcNow });

        Assert.Equal(leaderId, (await fixture.Standups.GetMeetingAsync(standup.Id, meetingDate))?.LeaderPersonId);
        Assert.Null(await fixture.Standups.GetMeetingAsync(standup.Id, meetingDate.AddDays(-1)));
    }

    [AzuriteFact]
    public async Task GetLockedDatesAsync_ReturnsTheLockedDatesInsideTheRangeAndNothingElse()
    {
        DateOnly monday = new(2026, 9, 14);
        Standup standup = new() { Name = "Locked week", CreatedUtc = DateTimeOffset.UtcNow };
        await fixture.Standups.UpsertAsync(standup);

        await UpsertMeetingAsync(standup.Id, monday, lockedUtc: DateTimeOffset.UtcNow);
        await UpsertMeetingAsync(standup.Id, monday.AddDays(1), lockedUtc: null);
        await UpsertMeetingAsync(standup.Id, monday.AddDays(9), lockedUtc: DateTimeOffset.UtcNow);

        IReadOnlyCollection<DateOnly> locked = await fixture.Standups.GetLockedDatesAsync(standup.Id, monday, monday.AddDays(6));

        Assert.Equal([monday], locked);
    }

    private Task UpsertMeetingAsync(Guid standupId, DateOnly meetingDate, DateTimeOffset? lockedUtc) =>
        fixture.Standups.UpsertMeetingAsync(new StandupMeeting { StandupId = standupId, MeetingDate = meetingDate, LockedUtc = lockedUtc, CreatedUtc = DateTimeOffset.UtcNow });

    [AzuriteFact]
    public async Task GetAllMembersAsync_CrossesEveryStandupPartition()
    {
        Standup first = new() { Name = "Scan first", CreatedUtc = DateTimeOffset.UtcNow };
        Standup second = new() { Name = "Scan second", CreatedUtc = DateTimeOffset.UtcNow };
        Guid firstPersonId = Guid.CreateVersion7();
        Guid secondPersonId = Guid.CreateVersion7();

        await fixture.Standups.UpsertAsync(first);
        await fixture.Standups.UpsertAsync(second);
        await fixture.Standups.UpsertMemberAsync(new StandupMember { StandupId = first.Id, PersonId = firstPersonId, DisplayOrder = 10 });
        await fixture.Standups.UpsertMemberAsync(new StandupMember { StandupId = second.Id, PersonId = secondPersonId, DisplayOrder = 10 });

        IReadOnlyList<StandupMember> all = await fixture.Standups.GetAllMembersAsync(RosterRole.Presenter);

        Assert.Contains(all, member => member.StandupId == first.Id && member.PersonId == firstPersonId);
        Assert.Contains(all, member => member.StandupId == second.Id && member.PersonId == secondPersonId);
    }

    [AzuriteFact]
    public async Task Rosters_AreScopedToTheirOwnStandup()
    {
        Standup first = new() { Name = "First", CreatedUtc = DateTimeOffset.UtcNow };
        Standup second = new() { Name = "Second", CreatedUtc = DateTimeOffset.UtcNow };
        Guid sharedPersonId = Guid.CreateVersion7();

        await fixture.Standups.UpsertAsync(first);
        await fixture.Standups.UpsertAsync(second);
        await fixture.Standups.UpsertMemberAsync(new StandupMember { StandupId = first.Id, PersonId = sharedPersonId, DisplayOrder = 10 });
        await fixture.Standups.UpsertMemberAsync(new StandupMember { StandupId = second.Id, PersonId = sharedPersonId, DisplayOrder = 30 });

        StandupMember firstMember = Assert.Single(await fixture.Standups.GetMembersAsync(first.Id, RosterRole.Presenter));
        StandupMember secondMember = Assert.Single(await fixture.Standups.GetMembersAsync(second.Id, RosterRole.Presenter));

        Assert.Equal(10, firstMember.DisplayOrder);
        Assert.Equal(30, secondMember.DisplayOrder);

        await fixture.Standups.RemoveMemberAsync(first.Id, sharedPersonId, RosterRole.Presenter);

        Assert.Empty(await fixture.Standups.GetMembersAsync(first.Id, RosterRole.Presenter));
        Assert.Single(await fixture.Standups.GetMembersAsync(second.Id, RosterRole.Presenter));
    }
}
