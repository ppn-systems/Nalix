using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Dashboard.Application.Abstractions;
using Nalix.Dashboard.Application.State;
using Nalix.Dashboard.Domain.Reports;
using Nalix.Dashboard.Infrastructure.Options;
using Nalix.Dashboard.Infrastructure.Serialization;
using Nalix.Observability.Contracts;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.Dashboard.Infrastructure.Nalix;

internal sealed class WebSocketAdminClient : IAdminClient, IAsyncDisposable
{
    private readonly AdminClientOptions _options;
    private readonly IDashboardStateWriter _state;
    private readonly IAdminSettingsStore _settings;
    private readonly ILogger<WebSocketAdminClient> _logger;
    private readonly SemaphoreSlim _sync = new(1, 1);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed via ResetSessionAsync in DisposeAsync")]
    private WebSocketSession? _session;
    private string? _apiKey;
    private bool _handshaken;
    private bool _authorized;

    public WebSocketAdminClient(
        IOptions<AdminClientOptions> options,
        IDashboardStateWriter state,
        IAdminSettingsStore settings,
        ILogger<WebSocketAdminClient> logger)
    {
        _options = options.Value;
        _state = state;
        _settings = settings;
        _logger = logger;
    }

    public async Task SetApiKeyAsync(string apiKey)
    {
        await _sync.WaitAsync().ConfigureAwait(false);
        try
        {
            _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
            _state.SetApiKeyConfigured(!string.IsNullOrWhiteSpace(_apiKey));
            _state.Log("INFO", _apiKey is null
                ? "API key cleared — session disconnected."
                : "API key configured — reconnecting.");
            await this.ResetSessionAsync().ConfigureAwait(false);
        }
        finally
        {
            _ = _sync.Release();
        }
    }

    public async Task RefreshAsync(RuntimeObservationTarget target, CancellationToken ct)
    {
        await _sync.WaitAsync(ct).ConfigureAwait(false);
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            WebSocketSession session = await this.EnsureConnectedAsync(ct).ConfigureAwait(false);
            DashboardReportSnapshot snapshot = await this.RequestReportAsync(session, target, ct).ConfigureAwait(false);
            _state.UpdateReport(snapshot);
            _state.Log("INFO", $"Report OK target={target} fields={snapshot.Data.Count.ToString(CultureInfo.InvariantCulture)} ms={sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            _logger.LogWarning(ex, "Failed to refresh target={Target}", target);
            _state.Log("WARN", $"Failed to refresh target={target}: {ex.Message}");
            _state.MarkDisconnected(ex.Message);
            await this.ResetSessionAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _ = _sync.Release();
        }
    }

    public async Task RefreshAllAsync(CancellationToken ct)
    {
        RuntimeObservationTarget[] targets =
        [
            RuntimeObservationTarget.DISPATCH,
            RuntimeObservationTarget.TASKS,
            RuntimeObservationTarget.BUFFERS,
            RuntimeObservationTarget.CONNECTIONS,
            RuntimeObservationTarget.INSTANCES,
            RuntimeObservationTarget.OBJECT_POOLS,
            RuntimeObservationTarget.CONNECTION_GUARD
        ];

        foreach (RuntimeObservationTarget target in targets)
        {
            ct.ThrowIfCancellationRequested();
            await this.RefreshAsync(target, ct).ConfigureAwait(false);
        }
    }

    public async Task PingAsync(CancellationToken ct)
    {
        await _sync.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            WebSocketSession session = await this.EnsureConnectedAsync(ct).ConfigureAwait(false);
            Application.Settings.AdminSettings cfg = await _settings.LoadAsync(ct).ConfigureAwait(false);
            double ms = await session.PingAsync(cfg.RequestTimeoutMs, ct).ConfigureAwait(false);
            _state.UpdatePing(ms);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            _logger.LogWarning(ex, "Ping failed.");
            _state.Log("WARN", $"Ping failed: {ex.Message}");
            _state.MarkDisconnected(ex.Message);
            await this.ResetSessionAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _ = _sync.Release();
        }
    }

    private async Task<WebSocketSession> EnsureConnectedAsync(CancellationToken ct)
    {
        if (_session?.IsConnected == true)
        {
            if (!_handshaken)
            {
                await _session.HandshakeAsync(ct).ConfigureAwait(false);
                _handshaken = true;
            }

            if (!_authorized)
            {
                await this.AuthorizeAsync(_session, ct).ConfigureAwait(false);
            }

            return _session;
        }

        await this.ResetSessionAsync().ConfigureAwait(false);

        Application.Settings.AdminSettings cfg = await _settings.LoadAsync(ct).ConfigureAwait(false);

        string host = !string.IsNullOrWhiteSpace(cfg.ServerHost)
            ? cfg.ServerHost.Trim()
            : _options.BackendHost;
        ushort port = cfg.ServerPort ?? _options.BackendPort;

        TransportOptions transport = new()
        {
            Address = host,
            Port = port,
            ConnectTimeoutMillis = cfg.RequestTimeoutMs,
            ServerPublicKey = _options.ServerPublicKey,
            ReconnectEnabled = false,
            KeepAliveIntervalMillis = 0
        };

        WebSocketTransportOptions wsOptions = new()
        {
            Path = cfg.WebSocketPath,
            UseTls = cfg.UseTls
        };

        WebSocketSession session = new(transport, wsOptions);
        session.OnDisconnected += (_, ex) =>
        {
            _state.Log("WARN", $"WebSocket disconnected: {ex.Message}");
            _state.MarkDisconnected(ex.Message);
        };
        session.OnError += (_, ex) =>
        {
            _logger.LogWarning(ex, "WebSocket session error.");
        };

        _session = session;
        _state.Log("INFO", $"Connecting WebSocket to {host}:{port.ToString(CultureInfo.InvariantCulture)}{cfg.WebSocketPath}");
        await session.ConnectAsync(ct: ct).ConfigureAwait(false);
        _state.Log("INFO", "WebSocket connected — starting handshake.");
        await session.HandshakeAsync(ct).ConfigureAwait(false);
        _handshaken = true;
        _state.Log("INFO", "Handshake complete.");
        _state.MarkConnected();

        try
        {
            await this.AuthorizeAsync(session, ct).ConfigureAwait(false);
        }
        catch
        {
            await this.ResetSessionAsync().ConfigureAwait(false);
            throw;
        }

        return session;
    }

    private async Task AuthorizeAsync(WebSocketSession session, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new NetworkException("Enter an API key before connecting.");
        }

        if (_apiKey.Length != 64)
        {
            throw new NetworkException("Invalid API key format. Expected a 64-character hexadecimal key.");
        }

        Bytes32 parsedKey;
        try
        {
            parsedKey = Bytes32.Parse(_apiKey);
        }
        catch
        {
            throw new NetworkException("Invalid API key format. Expected a 64-character hexadecimal key.");
        }

        using ObservabilityAccess request = ObservabilityAccess.Create();
        request.Initialize(ObservabilityAccessStage.REQUEST, accessKey: parsedKey);

        Application.Settings.AdminSettings cfg = await _settings.LoadAsync(ct).ConfigureAwait(false);

        using ObservabilityAccess response = await session.RequestAsync<ObservabilityAccess>(
            request,
            RequestOptions.Default.WithTimeout(cfg.RequestTimeoutMs).WithEncrypt(),
            predicate: p => p.Stage == ObservabilityAccessStage.RESPONSE,
            ct).ConfigureAwait(false);

        if (response.Reason != ProtocolReason.NONE || response.AccessLevel < PermissionLevel.SUPERVISOR)
        {
            _state.Log("WARN", $"Authority denied: reason={response.Reason} level={response.AccessLevel}");
            throw new NetworkException($"Authority grant failed: {response.Reason}");
        }

        _authorized = true;
        _state.Log("INFO", $"Authority granted: {response.AccessLevel}");
    }

    private async Task<DashboardReportSnapshot> RequestReportAsync(
        WebSocketSession session,
        RuntimeObservationTarget target,
        CancellationToken ct)
    {
        Application.Settings.AdminSettings cfg = await _settings.LoadAsync(ct).ConfigureAwait(false);

        using RuntimeObservation request = RuntimeObservation.Create();
        request.Initialize(RuntimeObservationStage.REQUEST, target);

        using RuntimeObservation response = await session.RequestAsync<RuntimeObservation>(
            request,
            RequestOptions.Default.WithTimeout(cfg.RequestTimeoutMs).WithEncrypt(),
            predicate: p => p.Stage == RuntimeObservationStage.RESPONSE && p.Target == target,
            ct).ConfigureAwait(false);

        ReadOnlyMemory<byte> ObservationData = response.ObservationData;
        IReadOnlyDictionary<string, object?> data = ReportJsonParser.Parse(ObservationData);
        return new DashboardReportSnapshot(target, response.Reason, ObservationData, data, DateTimeOffset.Now);
    }

    private async Task ResetSessionAsync()
    {
        _handshaken = false;
        _authorized = false;
        WebSocketSession? session = Interlocked.Exchange(ref _session, null);
        if (session is null)
        {
            return;
        }

        try
        {
            await session.DisconnectAsync().ConfigureAwait(false);
        }
        finally
        {
            session.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await this.ResetSessionAsync().ConfigureAwait(false);
        _sync.Dispose();
    }
}
