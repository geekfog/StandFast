using Microsoft.Net.Http.Headers;
using StandFast.Application.Dtos;
using StandFast.Application.Services;
using StandFast.Ui.Common;

namespace StandFast.Ui.Services;

/// <summary>
/// Serves profile photos as ordinary image responses, so the browser caches them and the Blazor circuit never carries the bytes. It carries no
/// authorisation metadata, so the fallback policy protects it exactly like a page.
/// </summary>
public static class PersonPhotoEndpoints
{
    /// <summary>Every photo address includes the photo's save time, so a cached copy can never be stale and may be kept for a year.</summary>
    private const string CacheControl = "private, max-age=31536000, immutable";

    public static void MapStandFastPersonPhotos(this WebApplication app) =>
        app.MapGet(UiRoutes.PersonPhotoTemplate, async (Guid personId, IPersonService people, HttpContext context, CancellationToken cancellationToken) =>
        {
            PersonPhotoDto? photo = await people.GetPhotoAsync(personId, cancellationToken);
            if (photo is null)
            {
                return Results.NotFound();
            }

            context.Response.Headers[HeaderNames.CacheControl] = CacheControl;
            return Results.File(photo.Content, photo.ContentType);
        });
}
