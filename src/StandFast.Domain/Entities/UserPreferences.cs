namespace StandFast.Domain.Entities;

/// <summary>
/// One signed-in user's settings. Keyed by the identity provider's subject identifier rather than by a person record, because signing in and being
/// on a roster are independent: anyone who can sign in gets settings, whether or not they appear on a board.
/// </summary>
public sealed class UserPreferences
{
    /// <summary>The appearance a user gets until they choose otherwise, and the value every unsaved or anonymous session falls back to.</summary>
    public const bool DefaultIsDarkMode = false;

    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The address the provider reports for this user, refreshed at sign-in so it follows a change made at the provider. It is what links the signed-in
    /// user to their own person record, which has no other point of contact with an identity.
    /// </summary>
    public string? Email { get; set; }

    public bool IsDarkMode { get; set; } = DefaultIsDarkMode;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>Storage concurrency token. Null for settings that have never been persisted.</summary>
    public string? ETag { get; set; }
}
