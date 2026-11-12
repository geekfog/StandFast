using Microsoft.JSInterop;
using StandFast.Domain.Common;

namespace StandFast.Ui.Services;

/// <summary>Reads time zone details only the viewer's browser knows. Callable only after the first interactive render, since prerendering has no browser.</summary>
public sealed class BrowserTimeZone(IJSRuntime js)
{
    private const string BrowserIdFunction = "standFastTimeZone.browserId";
    private const string AbbreviationFunction = "standFastTimeZone.abbreviation";

    private Task<TimeZoneInfo?>? lookup;

    /// <summary>The browser's time zone, read once per circuit, or null when the browser does not report one or this host cannot resolve it.</summary>
    public Task<TimeZoneInfo?> GetAsync() => lookup ??= InvokeOrNullAsync(async () => TimeZoneIds.Resolve(await js.InvokeAsync<string?>(BrowserIdFunction)));

    /// <summary>The lettered short name of <paramref name="zone"/> at <paramref name="instant"/>, such as CST or CDT, or null when the browser has none for it.</summary>
    public Task<string?> AbbreviationAsync(TimeZoneInfo zone, DateTimeOffset instant) =>
        InvokeOrNullAsync(() => js.InvokeAsync<string?>(AbbreviationFunction, TimeZoneIds.ToStored(zone.Id), instant.ToUnixTimeMilliseconds()).AsTask());

    private static async Task<T?> InvokeOrNullAsync<T>(Func<Task<T?>> call) where T : class
    {
        try
        {
            return await call();
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or TaskCanceledException)
        {
            return null;
        }
    }
}
