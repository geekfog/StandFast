using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StandFast.Application.Abstractions;
using StandFast.Application.Notifications;
using StandFast.Domain.Common;
using StandFast.Infrastructure.Configuration;

namespace StandFast.Infrastructure.Notifications;

/// <summary>
/// Carries board changes between server copies through Azure SignalR Service in Serverless mode. Each copy broadcasts what it saves and listens as an
/// ordinary client of the same hub, so the service holds one connection per copy. Browsers keep their own Blazor connection to the app and never touch the service.
/// </summary>
public sealed class AzureSignalRBoardChangeTransport(
    IOptions<AzureSignalROptions> options,
    ILoggerFactory loggerFactory,
    ILogger<AzureSignalRBoardChangeTransport> logger) : BackgroundService, IBoardChangeTransport
{
    private const string HubName = "boards";
    private const string ChangedMethod = "boardChanged";
    private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>Tells this copy's own broadcasts apart when the service echoes them back, since its boards already heard them.</summary>
    private readonly Guid instanceId = Guid.NewGuid();

    private ServiceManager? serviceManager;
    private ServiceHubContext? hubContext;
    private HubConnection? connection;

    public event Action<BoardChange>? Received;

    public async Task SendAsync(BoardChange change, CancellationToken cancellationToken = default)
    {
        // Until the first connection succeeds there is nowhere to send, and boards on this copy were told already.
        if (hubContext is not { } context)
        {
            return;
        }

        await context.Clients.All.SendCoreAsync(ChangedMethod, [instanceId, change.StandupId, MeetingCalendar.ToRouteValue(change.MeetingDate), change.OriginId], cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool hasWarned = false;

        while (true)
        {
            try
            {
                await ConnectAsync(stoppingToken);
                logger.LogInformation("Board changes are shared with other server copies through Azure SignalR hub {HubName}.", HubName);

                return;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                // Only the first failure is a warning, so a developer without the emulator running gets one line rather than one every retry.
                logger.Log(hasWarned ? LogLevel.Debug : LogLevel.Warning, exception,
                    "Azure SignalR is unreachable, so board changes stay on this server copy. Retrying every {RetryDelay}.", ConnectRetryDelay);
                hasWarned = true;
                await DisposeConnectionAsync();
            }

            await Task.Delay(ConnectRetryDelay, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await DisposeConnectionAsync();

        if (hubContext is not null)
        {
            await hubContext.DisposeAsync();
        }

        serviceManager?.Dispose();
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        serviceManager ??= new ServiceManagerBuilder()
            .WithOptions(managerOptions =>
            {
                managerOptions.ConnectionString = options.Value.ConnectionString;
                managerOptions.ServiceTransportType = ServiceTransportType.Transient;
            })
            .WithLoggerFactory(loggerFactory)
            .BuildServiceManager();

        hubContext ??= await serviceManager.CreateHubContextAsync(HubName, cancellationToken);
        NegotiationResponse negotiation = await NegotiateAsync(cancellationToken);

        // Reconnects ask for a fresh token, since the one from the first negotiation expires.
        connection = new HubConnectionBuilder()
            .WithUrl(negotiation.Url!, connectionOptions => connectionOptions.AccessTokenProvider = async () => (await NegotiateAsync(CancellationToken.None)).AccessToken)
            .WithAutomaticReconnect(new UnlimitedRetryPolicy())
            .Build();

        connection.On<Guid, Guid, string, Guid>(ChangedMethod, OnChanged);
        await connection.StartAsync(cancellationToken);
    }

    private ValueTask<NegotiationResponse> NegotiateAsync(CancellationToken cancellationToken) =>
        hubContext!.NegotiateAsync(new NegotiationOptions { UserId = instanceId.ToString() }, cancellationToken);

    private void OnChanged(Guid senderId, Guid standupId, string meetingDate, Guid originId)
    {
        if (senderId == instanceId || MeetingCalendar.ParseRouteValue(meetingDate) is not { } date)
        {
            return;
        }

        Received?.Invoke(new BoardChange(standupId, date, originId));
    }

    private async Task DisposeConnectionAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
            connection = null;
        }
    }

    /// <summary>Keeps reconnecting for as long as the app runs, backing off to <see cref="ConnectRetryDelay"/>, since a lost link only means boards on other copies go quiet.</summary>
    private sealed class UnlimitedRetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
            TimeSpan.FromSeconds(Math.Min(Math.Pow(2, retryContext.PreviousRetryCount), ConnectRetryDelay.TotalSeconds));
    }
}
