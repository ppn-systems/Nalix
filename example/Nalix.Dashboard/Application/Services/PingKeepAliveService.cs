using Nalix.Abstractions.Exceptions;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Application.State;

namespace Nalix.Dashboard.Application.Services;

internal sealed class PingKeepAliveService : IAsyncDisposable
{
    private readonly IAdminClient _client;
    private readonly IDashboardStateReader _stateReader;
    private readonly IDashboardStateWriter _stateWriter;
    private readonly IAdminSettingsStore _settings;

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pingTask;

    public PingKeepAliveService(
        IAdminClient client,
        IDashboardStateReader stateReader,
        IDashboardStateWriter stateWriter,
        IAdminSettingsStore settings)
    {
        _client = client;
        _stateReader = stateReader;
        _stateWriter = stateWriter;
        _settings = settings;
        _pingTask = this.RunPingLoopAsync(_cts.Token);
    }

    private async Task RunPingLoopAsync(CancellationToken ct)
    {
        int consecutiveFailures = 0;
        const int MaxFailures = 5;

        while (!ct.IsCancellationRequested)
        {
            Application.Settings.AdminSettings cfg = await _settings.LoadAsync(ct).ConfigureAwait(false);
            int intervalMs = Math.Max(1000, cfg.PingIntervalMs);

            if (!_stateReader.HasApiKey)
            {
                try { await Task.Delay(intervalMs, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
                continue;
            }

            try
            {
                await _client.PingAsync(ct).ConfigureAwait(false);
                consecutiveFailures = 0;
                _stateWriter.MarkConnected();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                consecutiveFailures++;
                _stateWriter.Log("WARN", $"Ping failed ({consecutiveFailures}/{MaxFailures}): {ex.Message}");
                if (consecutiveFailures >= MaxFailures)
                {
                    _stateWriter.MarkDisconnected(ex.Message);
                    consecutiveFailures = 0;
                }
            }

            try { await Task.Delay(intervalMs, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We suppress background ping loop exceptions during disposal.")]
    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try { await _pingTask.ConfigureAwait(false); } catch { }
        _cts.Dispose();
    }
}
