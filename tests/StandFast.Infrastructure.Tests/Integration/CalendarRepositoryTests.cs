using StandFast.Domain.Common;
using StandFast.Domain.Entities;
using StandFast.Domain.Enums;

namespace StandFast.Infrastructure.Tests.Integration;

/// <summary>Round-trips holidays, sprints, and days off through Azurite, including the overlap query and the cleanup that follows deleting their owner.</summary>
public sealed class CalendarRepositoryTests : IClassFixture<AzuriteTableFixture>
{
    private static readonly DateOnly Monday = new(2026, 9, 14);

    private readonly AzuriteTableFixture fixture;

    public CalendarRepositoryTests(AzuriteTableFixture fixture) => this.fixture = fixture;

    [AzuriteFact]
    public async Task Holidays_RoundTripAndDelete()
    {
        Holiday holiday = new() { Date = Monday, Name = "Founders Day", Duration = HolidayDuration.HalfDay, CreatedUtc = DateTimeOffset.UtcNow };

        await fixture.Holidays.UpsertAsync(holiday);
        Assert.Equal(HolidayDuration.HalfDay, Assert.Single(await fixture.Holidays.GetAllAsync()).Duration);

        await fixture.Holidays.DeleteAsync(holiday.Id);
        Assert.Null(await fixture.Holidays.GetAsync(holiday.Id));
    }

    [AzuriteFact]
    public async Task DaysOffForARange_ReturnEveryonesSpansThatTouchIt_AndNoOthers()
    {
        PersonDayOff before = await AddDayOffAsync(Guid.CreateVersion7(), Monday.AddDays(-7), Monday.AddDays(-1));
        PersonDayOff endsInside = await AddDayOffAsync(Guid.CreateVersion7(), Monday.AddDays(-3), Monday);
        PersonDayOff spansIt = await AddDayOffAsync(Guid.CreateVersion7(), Monday.AddDays(-3), Monday.AddDays(30));
        PersonDayOff startsOnTheEnd = await AddDayOffAsync(Guid.CreateVersion7(), Monday.AddDays(4), Monday.AddDays(8));

        HashSet<Guid> found = [.. (await fixture.People.GetDaysOffAsync(new DateRange(Monday, Monday.AddDays(4)))).Select(dayOff => dayOff.Id)];

        Assert.Superset(found, new HashSet<Guid> { endsInside.Id, spansIt.Id, startsOnTheEnd.Id }); // other tests in this class may add spans of their own
        Assert.DoesNotContain(before.Id, found);
    }

    [AzuriteFact]
    public async Task DeletingAPerson_AlsoRemovesTheirDaysOff()
    {
        Person person = new() { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com", CreatedUtc = DateTimeOffset.UtcNow };
        await fixture.People.UpsertAsync(person);
        await AddDayOffAsync(person.Id, Monday, Monday.AddDays(1));

        await fixture.People.DeleteAsync(person.Id);

        Assert.Empty(await fixture.People.GetDaysOffAsync(person.Id));
    }

    [AzuriteFact]
    public async Task DeletingAStandup_AlsoRemovesItsSprints()
    {
        Standup standup = new() { Name = "Sprinting", CreatedUtc = DateTimeOffset.UtcNow };
        await fixture.Standups.UpsertAsync(standup);
        await fixture.Standups.UpsertSprintAsync(new Sprint { StandupId = standup.Id, Name = "Sprint 29", StartDate = Monday, EndDate = Monday.AddDays(11), CreatedUtc = DateTimeOffset.UtcNow });

        Assert.Single(await fixture.Standups.GetSprintsAsync(standup.Id));

        await fixture.Standups.DeleteAsync(standup.Id);

        Assert.Empty(await fixture.Standups.GetSprintsAsync(standup.Id));
    }

    private async Task<PersonDayOff> AddDayOffAsync(Guid personId, DateOnly start, DateOnly end)
    {
        PersonDayOff dayOff = new() { PersonId = personId, StartDate = start, EndDate = end, CreatedUtc = DateTimeOffset.UtcNow };
        await fixture.People.UpsertDayOffAsync(dayOff);
        return dayOff;
    }
}
