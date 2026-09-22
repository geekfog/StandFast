namespace StandFast.Domain.Common;

/// <summary>
/// How an email address is stored and compared. A person on a roster and a signed-in user are two separate records that only their address ties
/// together, so both sides normalise and match on the same terms.
/// </summary>
public static class EmailAddress
{
    /// <summary>Stored form: trimmed and lower-cased, so one address written two ways is one value.</summary>
    public static string Normalise(string value) => value.Trim().ToLowerInvariant();

    /// <summary>Blank and absent mean the same thing for an optional address.</summary>
    public static string? NormaliseOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : Normalise(value);

    /// <summary>True when both addresses belong to the same person, however either was typed.</summary>
    public static bool Matches(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
