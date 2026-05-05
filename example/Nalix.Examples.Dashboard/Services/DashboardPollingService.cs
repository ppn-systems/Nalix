using Microsoft.Extensions.Options;

namespace Nalix.Examples.Dashboard.Services;

internal sealed class DashboardPollingService : BackgroundService
{
    private readonly DashboardTcpClient _client;
    private readonly DashboardState _state;
    private readonly DashboardOptions _options;

    public DashboardPollingService(
        DashboardTcpClient client,
        DashboardState state,
        IOptions<DashboardOptions> options)
    {
        _client = client;
        _state = state;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan pollInterval = TimeSpan.FromMilliseconds(Math.Max(100, _options.PollIntervalMilliseconds));
        TimeSpan pingInterval = TimeSpan.FromMilliseconds(Math.Max(1000, _options.PingIntervalMilliseconds));
        PeriodicTimer timer = new(pollInterval);
        DateTimeOffset nextPingAt = DateTimeOffset.MinValue;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_state.IsPollingPaused && _state.HasApiKey)
                {
                    await _client.RefreshAllAsync(stoppingToken).ConfigureAwait(false);
                }

                if (!_state.HasApiKey)
                {
                    nextPingAt = DateTimeOffset.MinValue;
                }
                else if (DateTimeOffset.UtcNow >= nextPingAt)
                {
                    await _client.PingAsync(stoppingToken).ConfigureAwait(false);
                    nextPingAt = DateTimeOffset.UtcNow.Add(pingInterval);
                }

                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            timer.Dispose();
        }
    }
}
