using StandFast.Application.Dtos;
using StandFast.Application.Tests.Fakes;
using StandFast.Domain.Entities;

namespace StandFast.Application.Tests.Services;

/// <summary>The in-memory store hands back the instance it holds, so setting a field on a person these tests added is what is stored.</summary>
public sealed class OrgChartReportTests
{
    private readonly ReportTestContext context = new();

    [Fact]
    public async Task PlacesEachPersonUnderTheirSupervisor_WithOneTreePerTopPerson_SortedByName()
    {
        Person zoe = await context.AddPersonAsync("Zoe", "Hale");
        Person ada = await context.AddPersonAsync("Ada", "Lovelace");
        Person cy = await context.AddPersonAsync("Cy", "Ng");
        Person ben = await context.AddPersonAsync("Ben", "Ruiz");
        Person dee = await context.AddPersonAsync("Dee", "Moss");
        (cy.SupervisorId, ben.SupervisorId, dee.SupervisorId) = (ada.Id, ada.Id, ben.Id);

        OrgChartDto chart = await context.Service.GetOrgChartAsync();

        Assert.Equal([ada.Id, zoe.Id], chart.Roots.Select(root => root.Person.Id));
        Assert.Equal([ben.Id, cy.Id], chart.Roots[0].Reports.Select(report => report.Person.Id));
        Assert.Equal([dee.Id], chart.Roots[0].Reports[0].Reports.Select(report => report.Person.Id));
        Assert.Equal(5, chart.PersonCount);
    }

    [Fact]
    public async Task KeepsAnInactiveSupervisorOfSomeoneActive_AndLeavesOutOtherInactivePeople()
    {
        Person ada = await context.AddPersonAsync("Ada", "Lovelace");
        Person ben = await context.AddPersonAsync("Ben", "Ruiz");
        Person cy = await context.AddPersonAsync("Cy", "Ng");
        (ada.IsActive, cy.IsActive, ben.SupervisorId) = (false, false, ada.Id);

        OrgChartDto chart = await context.Service.GetOrgChartAsync();

        OrgChartNodeDto root = Assert.Single(chart.Roots);
        Assert.Equal(ada.Id, root.Person.Id);
        Assert.False(root.Person.IsActive);
        Assert.Equal([ben.Id], root.Reports.Select(report => report.Person.Id));
    }

    [Fact]
    public async Task ASupervisorNoLongerInPeople_LeavesTheirReportAtTheTopOfATree()
    {
        Person ben = await context.AddPersonAsync("Ben", "Ruiz");
        ben.SupervisorId = Guid.CreateVersion7();

        OrgChartDto chart = await context.Service.GetOrgChartAsync();

        Assert.Equal([ben.Id], chart.Roots.Select(root => root.Person.Id));
    }

    [Fact]
    public async Task ALoopInStoredData_ShowsEachPersonOnce()
    {
        Person ada = await context.AddPersonAsync("Ada", "Lovelace");
        Person ben = await context.AddPersonAsync("Ben", "Ruiz");
        (ada.SupervisorId, ben.SupervisorId) = (ben.Id, ada.Id);

        OrgChartDto chart = await context.Service.GetOrgChartAsync();

        OrgChartNodeDto root = Assert.Single(chart.Roots);
        Assert.Equal(ada.Id, root.Person.Id);
        Assert.Equal([ben.Id], root.Reports.Select(report => report.Person.Id));
        Assert.Empty(root.Reports[0].Reports);
        Assert.Equal(2, chart.PersonCount);
    }

    [Fact]
    public async Task NobodyActive_HasNoData()
    {
        Person ada = await context.AddPersonAsync("Ada", "Lovelace");
        ada.IsActive = false;

        Assert.False((await context.Service.GetOrgChartAsync()).HasData);
    }
}
