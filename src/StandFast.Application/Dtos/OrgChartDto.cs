namespace StandFast.Application.Dtos;

/// <summary>Everyone in People arranged by supervisor: one tree per person who reports to nobody, sorted by name at every level.</summary>
/// <param name="PersonCount">Everyone placed in the trees, so the page can say how many people the chart covers.</param>
public sealed record OrgChartDto(IReadOnlyList<OrgChartNodeDto> Roots, int PersonCount)
{
    public bool HasData => Roots.Count > 0;
}

/// <summary>One person in the chart and the people who report to them.</summary>
public sealed record OrgChartNodeDto(PersonDto Person, IReadOnlyList<OrgChartNodeDto> Reports);
