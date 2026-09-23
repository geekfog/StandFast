using System.ComponentModel.DataAnnotations;

namespace StandFast.Application.Configuration;

/// <summary>Settings for closing a day's board to changes, bound from the <see cref="SectionName"/> configuration section.</summary>
public sealed class BoardLockOptions
{
    public const string SectionName = "BoardLock";

    /// <summary>How long after the day's last turn a lock still records the moment it was applied. Past it the lock is dated from that turn instead.</summary>
    [Range(1, int.MaxValue)]
    public int GraceMinutes { get; set; } = 60;

    /// <summary>Added to the day's last turn to date a lock applied past the grace window, which is what a board locked the next morning carries.</summary>
    [Range(1, int.MaxValue)]
    public int MinutesAfterLastTurn { get; set; } = 5;

    public TimeSpan Grace => TimeSpan.FromMinutes(GraceMinutes);

    public TimeSpan TailAfterLastTurn => TimeSpan.FromMinutes(MinutesAfterLastTurn);
}
