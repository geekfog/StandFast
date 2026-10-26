namespace StandFast.Domain.Common;

/// <summary>An inclusive span of dates, such as a sprint or someone's time off. Both ends count as part of the range.</summary>
public readonly record struct DateRange(DateOnly Start, DateOnly End)
{
    /// <summary>True when the range runs forward, which is the only shape a stored range takes.</summary>
    public bool IsValid => End >= Start;

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    public bool Overlaps(DateRange other) => Start <= other.End && other.Start <= End;

    /// <summary>Every date in the range in order. Empty when <see cref="End"/> falls before <see cref="Start"/>.</summary>
    public IEnumerable<DateOnly> Dates()
    {
        for (DateOnly date = Start; date <= End; date = date.AddDays(1))
        {
            yield return date;
        }
    }
}
