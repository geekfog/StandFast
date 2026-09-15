namespace StandFast.Domain.Common;

/// <summary>Single source of truth for field length and value limits. Validation, UI input limits, and storage mapping all read from here.</summary>
public static class DomainLimits
{
    public const int PersonNameMaxLength = 100;
    public const int EmailMaxLength = 256;
    public const int StandupNameMaxLength = 120;
    public const int DescriptionMaxLength = 1000;
    public const int TimeZoneIdMaxLength = 100;

    /// <summary>Max characters for a markdown field. Azure Table Storage caps a single string property at 32,768 characters; this leaves headroom for several markdown fields plus metadata inside the 1 MB entity limit.</summary>
    public const int MarkdownMaxLength = 16000;

    /// <summary>Rows fetched when resolving the current and immediately prior entry for a participant in one range query.</summary>
    public const int CurrentAndPriorFetchCount = 2;
}
