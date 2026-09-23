namespace StandFast.Application.Dtos;

/// <summary>What every report carries regardless of its shape: the standup it covers, the period it covers, and whether the period produced anything. The reports page renders one heading from this, whichever report is showing.</summary>
public interface IReportHeader
{
    string StandupName { get; }

    DateOnly From { get; }

    DateOnly To { get; }

    bool HasData { get; }
}
