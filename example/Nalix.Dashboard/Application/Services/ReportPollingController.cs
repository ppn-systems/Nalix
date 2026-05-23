using Nalix.Abstractions.Exceptions;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Application.State;
using Nalix.Observability.Contracts;

namespace Nalix.Dashboard.Application.Services;

internal sealed class ReportPollingController : IReportPollingController, IAsyncDisposable
{
    private readonly IAdminClient _client;
    private readonly IDashboardStateWriter _stateWriter;

    private const int NoTarget = -1;
    private volatile int _activeTargetRaw = NoTarget;
    private volatile string? _activeRoute;

    private RuntimeObservationTarget? _activeTarget
    {
        get => _activeTargetRaw == NoTarget ? null : (RuntimeObservationTarget)_activeTargetRaw;
        set => _activeTargetRaw = value.HasValue ? (int)value.Value : NoTarget;
    }
    private volatile bool _isPaused;
    private volatile int _intervalMs = 3000;

    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    private DateTimeOffset? _lastRequestUtc;
    private DateTimeOffset? _lastSuccessUtc;
    private DateTimeOffset? _lastFailureUtc;
    private string? _lastFailureMessage;
    private readonly Lock _timeLock = new();

    public event Action? Changed;

    public RuntimeObservationTarget? ActiveTarget => this._activeTarget;
    public string? ActiveRoute => _activeRoute;
    public bool IsPolling => _activeTargetRaw != NoTarget && !_isPaused && _pollTask is { IsCompleted: false };
    public bool IsPaused => _isPaused;
    public int CurrentIntervalMs => _intervalMs;

    public DateTimeOffset? LastRequestUtc { get { lock (_timeLock) { return _lastRequestUtc; } } }
    public DateTimeOffset? LastSuccessUtc { get { lock (_timeLock) { return _lastSuccessUtc; } } }
    public DateTimeOffset? LastFailureUtc { get { lock (_timeLock) { return _lastFailureUtc; } } }
    public string? LastFailureMessage { get { lock (_timeLock) { return _lastFailureMessage; } } }

    public ReportPollingController(IAdminClient client, IDashboardStateWriter stateWriter)
    {
        _client = client;
        _stateWriter = stateWriter;
    }

    public void Activate(RuntimeObservationTarget target, string route, int intervalMs)
    {
        _intervalMs = Math.Max(500, intervalMs);
        this._activeTarget = target;
        _activeRoute = route;
        _isPaused = false;
        this.RestartLoop();
        this.NotifyChanged();
    }

    public void Deactivate(string route)
    {
        if (!string.Equals(_activeRoute, route, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        this._activeTarget = null;
        _activeRoute = null;
        this.CancelLoop();
        this.NotifyChanged();
    }

    public void Pause()
    {
        if (_isPaused)
        {
            return;
        }

        _isPaused = true;
        this.CancelLoop();
        this.NotifyChanged();
    }

    public void Resume()
    {
        if (!_isPaused || !this._activeTarget.HasValue)
        {
            return;
        }

        _isPaused = false;
        this.RestartLoop();
        this.NotifyChanged();
    }

    public void SetInterval(int intervalMs)
    {
        _intervalMs = Math.Max(500, intervalMs);
        if (this._activeTarget.HasValue && !_isPaused)
        {
            this.RestartLoop();
        }
        this.NotifyChanged();
    }

    public async Task RequestOnceAsync(RuntimeObservationTarget target, CancellationToken ct)
    {
        lock (_timeLock) { _lastRequestUtc = DateTimeOffset.UtcNow; }
        this.NotifyChanged();
        try
        {
            await _client.RefreshAsync(target, ct).ConfigureAwait(false);
            lock (_timeLock) { _lastSuccessUtc = DateTimeOffset.UtcNow; _lastFailureMessage = null; }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            lock (_timeLock) { _lastFailureUtc = DateTimeOffset.UtcNow; _lastFailureMessage = ex.Message; }
            throw;
        }
        finally
        {
            this.NotifyChanged();
        }
    }

    private void RestartLoop()
    {
        CancellationTokenSource? old = Interlocked.Exchange(ref _pollCts, new CancellationTokenSource());
        old?.Cancel();
        old?.Dispose();
        _pollTask = this.RunLoopAsync(_pollCts.Token);
    }

    private void CancelLoop()
    {
        CancellationTokenSource? old = Interlocked.Exchange(ref _pollCts, null);
        old?.Cancel();
        old?.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && this._activeTarget is { } target)
        {
            lock (_timeLock) { _lastRequestUtc = DateTimeOffset.UtcNow; }
            this.NotifyChanged();

            try
            {
                await _client.RefreshAsync(target, ct).ConfigureAwait(false);
                lock (_timeLock) { _lastSuccessUtc = DateTimeOffset.UtcNow; _lastFailureMessage = null; }
                this.NotifyChanged();
                await Task.Delay(_intervalMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                lock (_timeLock) { _lastFailureUtc = DateTimeOffset.UtcNow; _lastFailureMessage = ex.Message; }
                _stateWriter.Log("WARN", $"Poll failed target={target}: {ex.Message}");
                this.NotifyChanged();
                int backoff = Math.Min(30_000, _intervalMs * 2);
                try { await Task.Delay(backoff, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            }
        }
    }

    private void NotifyChanged() => Changed?.Invoke();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We suppress exceptions during background task cleanup on disposal.")]
    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource? cts = Interlocked.Exchange(ref _pollCts, null);
        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
            cts.Dispose();
        }

        if (_pollTask is { } t)
        {
            try { await t.ConfigureAwait(false); } catch { }
        }
    }
}
