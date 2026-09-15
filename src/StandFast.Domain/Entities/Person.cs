namespace StandFast.Domain.Entities;

/// <summary>A person who can be added to one or more standups.</summary>
public sealed class Person
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>Storage concurrency token. Null for an entity that has never been persisted.</summary>
    public string? ETag { get; set; }

    /// <summary>The one authoritative way a person's name is rendered anywhere in the app.</summary>
    public string DisplayName => string.Join(' ', new[] { FirstName, LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>Initials used by avatars and compact board tiles.</summary>
    public string Initials => string.Concat(new[] { FirstName, LastName }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => char.ToUpperInvariant(part[0])));
}
