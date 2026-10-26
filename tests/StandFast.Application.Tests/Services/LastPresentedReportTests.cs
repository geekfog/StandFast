using StandFast.Application.Dtos;
using StandFast.Application.Tests.Fakes;
using StandFast.Domain.Entities;

namespace StandFast.Application.Tests.Services;

public sealed class LastPresentedReportTests
{
    private readonly ReportTestContext context = new();

    [Fact]
    public async Task ListsEachActivePresenter_NeverPresentedFirst_ThenTheLongestGap()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");
        Person ben = await context.AddPersonAsync("Ben", "Ruiz");
        Person cy = await context.AddPersonAsync("Cy", "Tan");
        foreach (Person person in new[] { amara, ben, cy })
        {
            await context.OnRosterAsync(person, ReportTestContext.Today.AddDays(-30));
        }

        await context.PresentedAsync(amara, ReportTestContext.Today.AddDays(-20), 0);
        await context.PresentedAsync(amara, ReportTestContext.Today.AddDays(-2), 0);
        await context.PresentedAsync(ben, ReportTestContext.Today.AddDays(-9), 0);

        LastPresentedDto report = await context.Service.GetLastPresentedAsync(context.TimeZone);

        Assert.Equal(
            [(cy.Id, (DateOnly?)null, (int?)null), (ben.Id, ReportTestContext.Today.AddDays(-9), 9), (amara.Id, ReportTestContext.Today.AddDays(-2), 2)],
            report.Rows.Select(row => (row.PersonId, row.LastPresented, row.DaysSince)));
        Assert.All(report.Rows, row => Assert.Equal(context.Standup.Name, row.StandupName));
    }

    [Fact]
    public async Task LeavesOutInactivePeople_PeopleOffTheRoster_AndInactiveStandups()
    {
        Person amara = await context.AddPersonAsync("Amara", "Okafor");
        Person ben = await context.AddPersonAsync("Ben", "Ruiz");
        await context.AddPersonAsync("Cy", "Tan");
        await context.OnRosterAsync(amara, ReportTestContext.Today);
        await context.OnRosterAsync(ben, ReportTestContext.Today);
        ben.IsActive = false;

        Assert.Equal([amara.Id], (await context.Service.GetLastPresentedAsync(context.TimeZone)).Rows.Select(row => row.PersonId));

        context.Standup.IsActive = false;

        Assert.False((await context.Service.GetLastPresentedAsync(context.TimeZone)).HasData);
    }
}
