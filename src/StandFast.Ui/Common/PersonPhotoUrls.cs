using System.Globalization;
using StandFast.Application.Dtos;

namespace StandFast.Ui.Common;

/// <summary>Addresses a profile photo is shown from: the photo endpoint for a saved photo, or an inline data URI for one not yet saved.</summary>
public static class PersonPhotoUrls
{
    /// <summary>
    /// The saved photo's endpoint, or null when the person has none. The save time is part of the address, so the browser caches each photo
    /// indefinitely and fetches a new one as soon as it is replaced.
    /// </summary>
    public static string? Saved(Guid personId, DateTimeOffset? photoSavedUtc) => photoSavedUtc is { } savedUtc
        ? $"{UiRoutes.PersonPhoto(personId)}?{UiRoutes.PhotoVersionQueryKey}={savedUtc.UtcTicks.ToString(CultureInfo.InvariantCulture)}"
        : null;

    public static string? Inline(PersonPhotoDto? photo) => photo is null ? null : $"data:{photo.ContentType};base64,{Convert.ToBase64String(photo.Content)}";
}
