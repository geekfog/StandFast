using System.Text.Json;
using Microsoft.Net.Http.Headers;
using MudBlazor.Utilities;
using StandFast.Ui.Common;

namespace StandFast.Ui.Services;

/// <summary>
/// Serves the web app manifest, built from the app's identity and theme so it stays in step with them. Browsers fetch it without cookies, so it is
/// anonymous; it holds nothing that the public icon files and page title do not already show.
/// </summary>
public static class ManifestEndpoints
{
    private const string ContentType = "application/manifest+json";
    private const string CacheControl = "public, max-age=86400";
    private const string SiteRoot = "/";

    private static readonly string Manifest = JsonSerializer.Serialize(
        new
        {
            id = SiteRoot,
            name = AppInfo.Name,
            short_name = AppInfo.Name,
            description = AppInfo.Tagline,
            start_url = SiteRoot,
            scope = SiteRoot,
            display = "standalone",
            theme_color = StandFastTheme.AppBarColor(isDarkMode: false),
            background_color = StandFastTheme.Default.PaletteLight.Background.ToString(MudColorOutputFormats.Hex),
            icons = new object[]
            {
                new { src = $"{SiteRoot}{AppInfo.IconAsset}", sizes = "any", type = "image/svg+xml" },
                new { src = $"{SiteRoot}{AppInfo.TouchIconAsset}", sizes = AppInfo.TouchIconSizes, type = "image/png" },
            },
        });

    public static void MapStandFastManifest(this WebApplication app) =>
        app.MapGet(UiRoutes.Manifest, (HttpContext context) =>
        {
            context.Response.Headers[HeaderNames.CacheControl] = CacheControl;
            return Results.Text(Manifest, ContentType);
        }).AllowAnonymous();
}
