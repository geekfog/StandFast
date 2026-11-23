using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StandFast.Application.Notifications;
using StandFast.Infrastructure.Configuration;
using StandFast.Infrastructure.Notifications;

namespace StandFast.Infrastructure.Tests.Integration;

public sealed class AzureSignalRBoardChangeTransportTests
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ResendInterval = TimeSpan.FromMilliseconds(250);

    [SignalREmulatorFact]
    public async Task SendAsync_ReachesTheOtherServerCopyButNotItself()
    {
        using CancellationTokenSource timeout = new(DeliveryTimeout);
        await using TransportHost sender = await TransportHost.StartAsync(timeout.Token);
        await using TransportHost listener = await TransportHost.StartAsync(timeout.Token);
        BoardChange change = new(Guid.NewGuid(), new DateOnly(2026, 11, 19), Guid.NewGuid());

        // Each copy connects in the background, so the change is sent again until the listener is connected and hears it.
        while (!listener.Received.Contains(change))
        {
            await sender.Transport.SendAsync(change, timeout.Token);
            await Task.Delay(ResendInterval, timeout.Token);
        }

        Assert.DoesNotContain(change, sender.Received);
    }

    private sealed class TransportHost : IAsyncDisposable
    {
        private TransportHost(AzureSignalRBoardChangeTransport transport)
        {
            Transport = transport;
            Transport.Received += Received.Add;
        }

        public AzureSignalRBoardChangeTransport Transport { get; }

        public ConcurrentBag<BoardChange> Received { get; } = [];

        public static async Task<TransportHost> StartAsync(CancellationToken cancellationToken)
        {
            AzureSignalRBoardChangeTransport transport = new(
                Options.Create(new AzureSignalROptions { ConnectionString = SignalREmulatorEndpoint.ConnectionString }),
                NullLoggerFactory.Instance,
                NullLogger<AzureSignalRBoardChangeTransport>.Instance);

            await transport.StartAsync(cancellationToken);
            return new TransportHost(transport);
        }

        public async ValueTask DisposeAsync()
        {
            await Transport.StopAsync(CancellationToken.None);
            Transport.Dispose();
        }
    }
}
