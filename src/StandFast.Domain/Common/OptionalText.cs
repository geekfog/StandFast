namespace StandFast.Domain.Common;

/// <summary>How optional free text is stored, so anything that compares typed text with stored text matches on the same terms.</summary>
public static class OptionalText
{
    /// <summary>Stored form: trimmed, with blank and absent meaning the same thing.</summary>
    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>True when storing either value would store the same thing.</summary>
    public static bool Matches(string? left, string? right) => Normalize(left) == Normalize(right);
}
