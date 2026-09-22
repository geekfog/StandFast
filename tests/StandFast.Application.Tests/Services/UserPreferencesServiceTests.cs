using StandFast.Application.Dtos;
using StandFast.Application.Services;
using StandFast.Application.Tests.Fakes;

namespace StandFast.Application.Tests.Services;

/// <summary>Covers what a session falls back to when nothing has been saved, that saving a choice is what changes it, and that the address follows the provider.</summary>
public sealed class UserPreferencesServiceTests
{
    private const string UserId = "kp_5f2c7e1a";
    private const string OtherUserId = "kp_9b3d4c22";
    private const string Email = "ada.lovelace@example.com";

    private readonly InMemoryUserPreferencesRepository repository = new();
    private readonly UserPreferencesService service;

    public UserPreferencesServiceTests() => service = new UserPreferencesService(repository, new FixedClock(new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void DefaultIsLightModeAndMatchesNobody()
    {
        Assert.False(UserPreferencesDto.Default.IsDarkMode);
        Assert.False(UserPreferencesDto.Default.IsSelf(Email));
    }

    [Fact]
    public async Task UserWithNothingSavedGetsTheDefault() => Assert.Equal(UserPreferencesDto.Default, await service.LoadAsync(UserId, email: null));

    [Fact]
    public async Task SavedChoiceIsReturnedForThatUserOnly()
    {
        await service.SetDarkModeAsync(UserId, isDarkMode: true);

        Assert.True((await service.LoadAsync(UserId, Email)).IsDarkMode);
        Assert.False((await service.LoadAsync(OtherUserId, Email)).IsDarkMode);
    }

    [Fact]
    public async Task ChoiceCanBeChangedBack()
    {
        await service.SetDarkModeAsync(UserId, isDarkMode: true);
        await service.SetDarkModeAsync(UserId, isDarkMode: false);

        Assert.False((await service.LoadAsync(UserId, Email)).IsDarkMode);
    }

    [Fact]
    public async Task AddressIsStoredOnFirstLoadAndMatchesHowItIsWritten()
    {
        UserPreferencesDto loaded = await service.LoadAsync(UserId, "  Ada.Lovelace@Example.com  ");

        Assert.Equal(Email, loaded.Email);
        Assert.True(loaded.IsSelf("ADA.LOVELACE@example.com"));
        Assert.False(loaded.IsSelf("grace.hopper@example.com"));
    }

    [Fact]
    public async Task AddressFollowsAChangeAtTheProvider()
    {
        await service.LoadAsync(UserId, Email);

        Assert.Equal("ada.byron@example.com", (await service.LoadAsync(UserId, "ada.byron@example.com")).Email);
    }

    /// <summary>A provider that reports no address on a later sign-in leaves the stored one alone rather than clearing it.</summary>
    [Fact]
    public async Task AddressSurvivesASignInThatReportsNone()
    {
        await service.LoadAsync(UserId, Email);

        Assert.Equal(Email, (await service.LoadAsync(UserId, email: null)).Email);
    }

    [Fact]
    public async Task AddressSurvivesChangingTheAppearance()
    {
        await service.LoadAsync(UserId, Email);
        await service.SetDarkModeAsync(UserId, isDarkMode: true);

        UserPreferencesDto loaded = await service.LoadAsync(UserId, Email);

        Assert.Equal(Email, loaded.Email);
        Assert.True(loaded.IsDarkMode);
    }

    /// <summary>An anonymous session has no key to save under, so it neither reads nor writes anything and marks nobody on the board.</summary>
    [Fact]
    public async Task AnonymousSessionGetsTheDefaultAndSavesNothing()
    {
        await service.SetDarkModeAsync(userId: null, isDarkMode: true);

        Assert.Equal(UserPreferencesDto.Default, await service.LoadAsync(null, Email));
    }
}
