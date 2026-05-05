using Microsoft.Extensions.Options;
using Nalix.Examples.Dashboard.Application.Abstractions;
using Nalix.Examples.Dashboard.Application.Options;
using Nalix.Examples.Dashboard.Application.State;

namespace Nalix.Examples.Dashboard.Application.Polling;

internal sealed class DashboardPollingService : BackgroundService
{
    private readonly IDashboardClient _client;
    private readonly IDashboardStateReader _state;
    private readonly IDashboardStateWriter _stateWriter;
    private readonly DashboardOptions _options;

    public DashboardPollingService(
        IDashboardClient client,
        IDashboardStateReader state,
        IDashboardStateWriter stateWriter,
        IOptions<DashboardOptions> options)
    {
        _client = client;
        _state = state;
        _stateWriter = stateWriter;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan pollInterval = TimeSpan.FromMilliseconds(Math.Max(100, _options.PollIntervalMilliseconds));
        TimeSpan pingInterval = TimeSpan.FromMilliseconds(Math.Max(1000, _options.PingIntervalMilliseconds));
        PeriodicTimer timer = new(pollInterval);
        DateTimeOffset nextPingAt = DateTimeOffset.MinValue;
        BooleanStateLog waitingForApiKeyLog = new();
        BooleanStateLog pausedLog = new();
        BooleanStateLog idleTargetLog = new();

        _stateWriter.Log("INFO", $"Dashboard polling service started poll_ms={(int)pollInterval.TotalMilliseconds} ping_ms={(int)pingInterval.TotalMilliseconds}.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_state.HasApiKey)
                {
                    waitingForApiKeyLog.WriteOnce(_stateWriter, "INFO", "Dashboard polling waiting reason=api_key_missing.");
                    nextPingAt = DateTimeOffset.MinValue;
                }
                else
                {
                    waitingForApiKeyLog.Reset();
                }

                if (_state.IsPollingPaused)
                {
                    pausedLog.WriteOnce(_stateWriter, "INFO", "Report polling loop is paused.");
                }
                else
                {
                    pausedLog.Reset();
                }

                if (!_state.IsPollingPaused &&
                    _state.HasApiKey &&
                    _state.ActiveReportTarget is { } activeTarget)
                {
                    idleTargetLog.Reset();
                    await _client.RefreshAsync(activeTarget, stoppingToken).ConfigureAwait(false);
                }
                else if (!_state.IsPollingPaused && _state.HasApiKey && _state.ActiveReportTarget is null)
                {
                    idleTargetLog.WriteOnce(_stateWriter, "DEBUG", "Report polling idle reason=no_active_report_target.");
                }

                if (_state.HasApiKey && DateTimeOffset.UtcNow >= nextPingAt)
                {
                    await _client.PingAsync(stoppingToken).ConfigureAwait(false);
                    nextPingAt = DateTimeOffset.UtcNow.Add(pingInterval);
                }

                _ = await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _stateWriter.Log("INFO", "Dashboard polling service stopping.");
        }
        catch (Exception ex)
        {
            _stateWriter.Log("ERROR", $"Dashboard polling service failed error_type={ex.GetType().Name} message=\"{ex.Message}\".");
            throw;
        }
        finally
        {
            timer.Dispose();
        }
    }

    private struct BooleanStateLog
    {
        private bool _written;

        public void WriteOnce(IDashboardStateWriter state, string level, string message)
        {
            if (_written)
            {
                return;
            }

            state.Log(level, message);
            _written = true;
        }

        public void Reset() => _written = false;
    }
}
