namespace StandFast.Domain.Entities;

/// <summary>A person who can be added to one or more standups.</summary>
public sealed class Person
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>Optional override for how this person is shown. Covers preferred names, nicknames, and anyone who goes by something other than their legal name.</summary>
    public string? DisplayAs { get; set; }

    /// <summary>Free-form markdown about the person, such as working hours or a standing note for whoever is running the standup.</summary>
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>Storage concurrency token. Null for an entity that has never been persisted.</summary>
    public string? ETag { get; set; }

    /// <summary>The one authoritative way a person's name is rendered anywhere in the app. <see cref="DisplayAs"/> wins when it is set.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(DisplayAs) ? FullName : DisplayAs.Trim();

    /// <summary>First and last name as recorded, regardless of any display override. Used where the real name is what matters.</summary>
    public string FullName => string.Join(' ', new[] { FirstName, LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>Initials used by avatars and compact board tiles. Derived from <see cref="DisplayName"/> so the avatar always matches the name beside it.</summary>
    public string Initials => string.Concat(DisplayName
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Take(2)
        .Select(part => char.ToUpperInvariant(part[0])));
}
