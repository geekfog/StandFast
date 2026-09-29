namespace StandFast.Domain.Entities;

/// <summary>A person's profile photo. Kept apart from <see cref="Person"/> so listing people never reads the image bytes.</summary>
public sealed class PersonPhoto
{
    public Guid PersonId { get; set; }

    /// <summary>Media type of <see cref="Content"/>, for example <c>image/jpeg</c>.</summary>
    public string ContentType { get; set; } = string.Empty;

    public byte[] Content { get; set; } = [];
}
