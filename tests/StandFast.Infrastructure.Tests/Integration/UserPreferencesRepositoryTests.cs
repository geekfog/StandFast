using Azure.Data.Tables;
using StandFast.Domain.Entities;
using StandFast.Infrastructure.Storage;

namespace StandFast.Infrastructure.Tests.Integration;

/// <summary>Round-trips a user's settings through Azurite, including the absent-settings case the default appearance depends on.</summary>
public sealed class UserPreferencesRepositoryTests : IClassFixture<AzuriteTableFixture>
{
    private readonly AzuriteTableFixture fixture;

    public UserPreferencesRepositoryTests(AzuriteTableFixture fixture) => this.fixture = fixture;

    [AzuriteFact]
    public async Task SettingsRoundTripAndOverwrite()
    {
        UserPreferences preferences = new() { UserId = $"kp_{Guid.NewGuid():N}", Email = "ada.lovelace@example.com", IsDarkMode = true, CreatedUtc = DateTimeOffset.UtcNow };

        await fixture.Preferences.UpsertAsync(preferences);
        Assert.True((await fixture.Preferences.GetAsync(preferences.UserId))?.IsDarkMode);

        preferences.IsDarkMode = false;
        preferences.IsNavigationExpanded = false;
        await fixture.Preferences.UpsertAsync(preferences);

        UserPreferences? stored = await fixture.Preferences.GetAsync(preferences.UserId);
        Assert.False(stored?.IsDarkMode);
        Assert.False(stored?.IsNavigationExpanded);
        Assert.Equal(preferences.UserId, stored?.UserId);
        Assert.Equal(preferences.Email, stored?.Email);
    }

    /// <summary>A row saved before the navigation column existed carries only the appearance, and reads back with the navigation menu expanded.</summary>
    [AzuriteFact]
    public async Task RowWithoutNavigationColumnReadsAsExpanded()
    {
        string userId = $"kp_{Guid.NewGuid():N}";
        TableClient table = await fixture.Tables.GetAsync(StorageNames.UserPreferences);
        TableEntity row = new(StorageKeys.UserPreferencesPartition, StorageKeys.UserPreferencesRowKey(userId))
        {
            [nameof(UserPreferences.UserId)] = userId,
            [nameof(UserPreferences.IsDarkMode)] = true,
            [nameof(UserPreferences.CreatedUtc)] = DateTimeOffset.UtcNow,
        };

        await table.AddEntityAsync(row);
        UserPreferences? stored = await fixture.Preferences.GetAsync(userId);

        Assert.Equal(UserPreferences.DefaultIsNavigationExpanded, stored?.IsNavigationExpanded);
        Assert.True(stored?.IsDarkMode);
    }

    [AzuriteFact]
    public async Task UnknownUserHasNoStoredSettings() => Assert.Null(await fixture.Preferences.GetAsync($"kp_{Guid.NewGuid():N}"));

    /// <summary>A subject identifier carrying characters Azure Table keys reject still stores and reads back under its own value.</summary>
    [AzuriteFact]
    public async Task SubjectWithKeyUnsafeCharactersRoundTrips()
    {
        string userId = $"provider/{Guid.NewGuid():N}#1";
        UserPreferences preferences = new() { UserId = userId, IsDarkMode = true, CreatedUtc = DateTimeOffset.UtcNow };

        await fixture.Preferences.UpsertAsync(preferences);
        UserPreferences? stored = await fixture.Preferences.GetAsync(userId);

        Assert.Equal(userId, stored?.UserId);
        Assert.True(stored?.IsDarkMode);
    }
}
