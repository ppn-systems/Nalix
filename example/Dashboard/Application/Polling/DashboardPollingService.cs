using Dashboard.Application.Abstractions;
using Dashboard.Application.Options;
using Dashboard.Application.State;
using Microsoft.Extensions.Options;
using Nalix.Abstractions.Exceptions;

namespace Dashboard.Application.Polling;

internal sealed class DashboardPollingService : BackgroundService
{
    private readonly IDashboardClient _client;
    private readonly IDashboardStateReader _state;
    private readonly IDashboardStateWriter _stateWriter;
    private readonly DashboardOptions _options;

    // Đếm số lần lỗi liên tiếp để tránh spam log
    private int _consecutiveFailures;
    private const int MaxFailuresBeforeDisconnect = 1000; // Sau 1000 lần lỗi liên tiếp mới coi là mất kết nối

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
        _ = TimeSpan.FromMilliseconds(Math.Max(1000, _state.PingIntervalMs));
        PeriodicTimer timer = new(TimeSpan.FromMilliseconds(Math.Max(100, _state.PollIntervalMs)));
        DateTimeOffset nextPingAt = DateTimeOffset.MinValue;

        BooleanStateLog waitingForApiKeyLog = new();
        BooleanStateLog pausedLog = new();
        BooleanStateLog idleTargetLog = new();

        _stateWriter.Log("INFO", $"Dashboard polling service started poll_ms={_state.PollIntervalMs} ping_ms={_state.PingIntervalMs}.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                int currentPollMs = _state.PollIntervalMs;
                int currentPingMs = _state.PingIntervalMs;

                TimeSpan desiredPoll = TimeSpan.FromMilliseconds(Math.Max(100, currentPollMs));
                if (timer.Period != desiredPoll)
                {
                    timer.Dispose();
                    timer = new PeriodicTimer(desiredPoll);
                    _stateWriter.Log("DEBUG", $"Poll interval applied value={currentPollMs}ms.");
                }

                TimeSpan pingInterval = TimeSpan.FromMilliseconds(Math.Max(1000, currentPingMs));

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

                // ==================== REFRESH ====================
                if (!_state.IsPollingPaused && _state.HasApiKey && _state.ActiveReportTarget is { } activeTarget)
                {
                    idleTargetLog.Reset();

                    try
                    {
                        await _client.RefreshAsync(activeTarget, stoppingToken).ConfigureAwait(false);
                        _consecutiveFailures = 0;                    // Reset đếm lỗi
                        _stateWriter.MarkConnected();                // Thành công
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // Đang dừng service
                    }
                    catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                    {
                        _consecutiveFailures++;
                        _stateWriter.Log("WARN", $"Refresh timeout/fail target={activeTarget} ({_consecutiveFailures}/{MaxFailuresBeforeDisconnect}) error={ex.GetType().Name}: {ex.Message}");

                        // CHỈ mark disconnected khi lỗi liên tiếp quá nhiều
                        if (_consecutiveFailures >= MaxFailuresBeforeDisconnect)
                        {
                            _stateWriter.MarkDisconnected(ex.Message);
                            _consecutiveFailures = 0; // reset để lần sau thử lại
                        }
                    }
                }
                else if (!_state.IsPollingPaused && _state.HasApiKey && _state.ActiveReportTarget is null)
                {
                    idleTargetLog.WriteOnce(_stateWriter, "DEBUG", "Report polling idle reason=no_active_report_target.");
                }

                // ==================== PING ====================
                if (_state.HasApiKey && DateTimeOffset.UtcNow >= nextPingAt)
                {
                    try
                    {
                        await _client.PingAsync(stoppingToken).ConfigureAwait(false);
                        _consecutiveFailures = 0;
                        _stateWriter.MarkConnected();
                        nextPingAt = DateTimeOffset.UtcNow.Add(pingInterval);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                    }
                    catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                    {
                        _consecutiveFailures++;
                        _stateWriter.Log("WARN", $"Ping failed ({_consecutiveFailures}/{MaxFailuresBeforeDisconnect}) error={ex.GetType().Name}: {ex.Message}");

                        if (_consecutiveFailures >= MaxFailuresBeforeDisconnect)
                        {
                            _stateWriter.MarkDisconnected(ex.Message);
                            _consecutiveFailures = 0;
                            nextPingAt = DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(3));
                        }
                        else
                        {
                            // Timeout tạm thời → thử lại nhanh hơn
                            nextPingAt = DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(1));
                        }
                    }
                }

                _ = await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _stateWriter.Log("INFO", "Dashboard polling service stopping.");
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            _stateWriter.Log("ERROR", $"Dashboard polling service crashed: {ex.Message}");
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
