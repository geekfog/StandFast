namespace StandFast.Infrastructure.Tests.Integration;

/// <summary>
/// The emulator probe decides whether the integration tests skip or run, and it is called from an attribute constructor, so a throw there fails
/// discovery for every test in the class instead of skipping it. These run everywhere, with or without Azurite.
/// </summary>
public sealed class AzuriteEndpointTests
{
    /// <summary>A port nothing listens on, so the connection is refused outright instead of timing out.</summary>
    private const int ClosedPort = 10099;

    private const string UnroutableHost = "0.0.0.1";

    [Fact]
    public void CanConnect_ReturnsFalse_ForARefusedConnection() =>
        Assert.False(AzuriteEndpoint.CanConnect(AzuriteEndpoint.Host, ClosedPort));

    [Fact]
    public void CanConnect_ReturnsFalse_ForAHostItCannotReach() =>
        Assert.False(AzuriteEndpoint.CanConnect(UnroutableHost, AzuriteEndpoint.TablePort));

    [Fact]
    public void IsAvailable_DoesNotThrow_WhicheverWayItAnswers()
    {
        bool first = AzuriteEndpoint.IsAvailable;

        Assert.Equal(first, AzuriteEndpoint.IsAvailable);
    }
}
