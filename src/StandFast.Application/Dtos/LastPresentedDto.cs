namespace StandFast.Application.Dtos;

/// <summary>Each active presenter on each active standup with the last date they presented there, longest gap first.</summary>
public sealed record LastPresentedDto(IReadOnlyList<LastPresentedRowDto> Rows)
{
    public bool HasData => Rows.Count > 0;
}

/// <summary>One presenter on one standup.</summary>
/// <param name="LastPresented">The most recent date they presented at this standup, or null when they never have.</param>
/// <param name="DaysSince">Calendar days from <paramref name="LastPresented"/> to today in the standup's time zone, or null when they never presented.</param>
public sealed record LastPresentedRowDto(Guid StandupId, string StandupName, Guid PersonId, string DisplayName, DateOnly? LastPresented, int? DaysSince);
