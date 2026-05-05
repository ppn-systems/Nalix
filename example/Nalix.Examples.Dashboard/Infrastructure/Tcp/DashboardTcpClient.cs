using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Options;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.Codec.DataFrames;
using Nalix.Examples.Contracts.Packets;
using Nalix.Examples.Dashboard.Application.Abstractions;
using Nalix.Examples.Dashboard.Application.Options;
using Nalix.Examples.Dashboard.Application.Reports;
using Nalix.Examples.Dashboard.Application.State;
using Nalix.Examples.Dashboard.Domain.Reports;
using Nalix.Examples.Dashboard.Infrastructure.Security;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.Examples.Dashboard.Infrastructure.Tcp;

internal sealed class DashboardTcpClient : IDashboardClient, IAsyncDisposable
{
    private readonly DashboardOptions _options;
    private readonly IDashboardStateWriter _state;
    private readonly IServerPublicKeyResolver _publicKeyResolver;
    private readonly ILogger<DashboardTcpClient> _logger;
    private readonly SemaphoreSlim _sync = new(1, 1);
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed through ResetSessionAsync using Interlocked.Exchange.")]
    private TcpSession? _session;
    private string? _apiKey;
    private bool _handshaken;
    private bool _authorized;

    private string Endpoint => $"{_options.BackendAddress}:{_options.BackendPort.ToString(CultureInfo.InvariantCulture)}";

    public DashboardTcpClient(
        IOptions<DashboardOptions> options,
        IDashboardStateWriter state,
        IServerPublicKeyResolver publicKeyResolver,
        ILogger<DashboardTcpClient> logger)
    {
        _options = options.Value;
        _state = state;
        _publicKeyResolver = publicKeyResolver;
        _logger = logger;
        _state.SetEndpoint(this.Endpoint);
    }

    public async Task SetApiKeyAsync(string apiKey)
    {
        await _sync.WaitAsync().ConfigureAwait(false);
        try
        {
            _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
            _state.SetApiKeyConfigured(!string.IsNullOrWhiteSpace(_apiKey));
            _state.Log("INFO", _apiKey is null
                ? "API key cleared action=disconnect_session."
                : "API key configured action=reconnect_session.");
            await this.ResetSessionAsync().ConfigureAwait(false);
        }
        finally
        {
            _ = _sync.Release();
        }
    }

    public async Task RefreshAsync(GenerationReportTarget target, CancellationToken ct)
    {
        await _sync.WaitAsync(ct).ConfigureAwait(false);
        Stopwatch elapsed = Stopwatch.StartNew();
        try
        {
            _state.Log("DEBUG", $"Report refresh started target={target} endpoint={this.Endpoint}.");
            TcpSession session = await this.EnsureConnectedAsync(ct).ConfigureAwait(false);
            DashboardReportSnapshot snapshot = await this.RequestReportAsync(session, target, ct).ConfigureAwait(false);
            _state.UpdateReport(snapshot);
            _state.Log("INFO", $"Report refresh completed target={target} reason={snapshot.Reason} fields={snapshot.Data.Count.ToString(CultureInfo.InvariantCulture)} elapsed_ms={elapsed.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)}.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            _logger.LogWarning(ex, "Dashboard refresh failed for target {Target}.", target);
            _state.Log("WARN", $"Report refresh failed target={target} elapsed_ms={elapsed.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)}.");
            _state.MarkDisconnected(ex.Message);
            await this.ResetSessionAsync().ConfigureAwait(false);
        }
        finally
        {
            _ = _sync.Release();
        }
    }

    public async Task RefreshAllAsync(CancellationToken ct)
    {
        _state.Log("INFO", $"Report refresh-all started targets={DashboardReportTargets.Count.ToString(CultureInfo.InvariantCulture)}.");
        foreach (GenerationReportTarget target in DashboardReportTargets.All)
        {
            ct.ThrowIfCancellationRequested();
            await this.RefreshAsync(target, ct).ConfigureAwait(false);
        }

        _state.Log("INFO", "Report refresh-all completed.");
    }

    public async Task PingAsync(CancellationToken ct)
    {
        await _sync.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _state.Log("DEBUG", "Sending keepalive ping.");
            TcpSession session = await this.EnsureConnectedAsync(ct).ConfigureAwait(false);
            double milliseconds = await session.PingAsync(_options.RequestTimeoutMilliseconds, ct).ConfigureAwait(false);
            _state.UpdatePing(milliseconds);
            _state.Log("INFO", $"Keepalive ping OK: {milliseconds.ToString("F1", CultureInfo.InvariantCulture)} ms.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            _logger.LogWarning(ex, "Dashboard keepalive ping failed.");
            _state.Log("WARN", $"Keepalive ping failed: {ex.GetType().Name}: {ex.Message}");
            _state.MarkDisconnected(ex.Message);
            await this.ResetSessionAsync().ConfigureAwait(false);
        }
        finally
        {
            _ = _sync.Release();
        }
    }

    private async Task<TcpSession> EnsureConnectedAsync(CancellationToken ct)
    {
        if (_session?.IsConnected == true)
        {
            if (!_handshaken)
            {
                _state.Log("INFO", "Connected session exists; performing handshake.");
                await _session.HandshakeAsync(ct).ConfigureAwait(false);
                _handshaken = true;
                _state.Log("INFO", "Handshake complete.");
            }

            if (!_authorized)
            {
                await this.AuthorizeAsync(_session, ct).ConfigureAwait(false);
            }

            return _session;
        }

        await this.ResetSessionAsync().ConfigureAwait(false);

        PacketRegistry catalog = DashboardPacketCatalogFactory.Create();
        string serverPublicKey = _publicKeyResolver.Resolve(_options);

        TransportOptions transport = new()
        {
            Address = _options.BackendAddress,
            Port = _options.BackendPort,
            ConnectTimeoutMillis = _options.RequestTimeoutMilliseconds,
            ServerPublicKey = serverPublicKey,
            ReconnectEnabled = false,
            KeepAliveIntervalMillis = 0
        };

        TcpSession session = new(transport, catalog);
        session.OnDisconnected += (_, ex) =>
        {
            _state.Log("WARN", $"TCP disconnected: {ex.Message}");
            _state.MarkDisconnected(ex.Message);
        };
        session.OnError += (_, ex) =>
        {
            _logger.LogWarning(ex, "Dashboard TCP session error.");
            _state.Log("WARN", $"TCP session error: {ex.GetType().Name}: {ex.Message}");
        };

        _session = session;
        _state.Log("INFO", $"Connecting TCP session to {_options.BackendAddress}:{_options.BackendPort.ToString(CultureInfo.InvariantCulture)}.");
        await session.ConnectAsync(ct: ct).ConfigureAwait(false);
        _state.Log("INFO", "TCP connected; performing handshake.");
        await session.HandshakeAsync(ct).ConfigureAwait(false);
        _handshaken = true;
        _state.Log("INFO", "Handshake complete.");
        _state.MarkConnected();

        await this.AuthorizeAsync(session, ct).ConfigureAwait(false);
        return session;
    }

    private async Task AuthorizeAsync(TcpSession session, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new NetworkException("Enter an API key before connecting to the backend.");
        }

        using AuthorityGrant request = AuthorityGrant.Create();
        request.Initialize(AuthorityGrantStage.REQUEST, key: _apiKey);

        _state.Log("INFO", "Authorizing dashboard session.");
        using AuthorityGrant response = await session.RequestAsync<AuthorityGrant>(
            request,
            RequestOptions.Default
                .WithTimeout(_options.RequestTimeoutMilliseconds)
                .WithEncrypt(),
            predicate: p => p.Stage == AuthorityGrantStage.RESPONSE,
            ct).ConfigureAwait(false);

        if (response.Reason != ProtocolReason.NONE || response.GrantedLevel < PermissionLevel.SYSTEM_ADMINISTRATOR)
        {
            _state.Log("WARN", $"Authority grant rejected: reason={response.Reason}, level={response.GrantedLevel}.");
            throw new NetworkException($"Authority grant failed: {response.Reason}");
        }

        _authorized = true;
        _state.Log("INFO", $"Authority granted: {response.GrantedLevel}.");
    }

    private async Task<DashboardReportSnapshot> RequestReportAsync(
        TcpSession session,
        GenerationReportTarget target,
        CancellationToken ct)
    {
        using GenerationReport request = GenerationReport.Create();
        request.Initialize(GenerationReportStage.REQUEST, target);

        using GenerationReport response = await session.RequestAsync<GenerationReport>(
            request,
            RequestOptions.Default
                .WithTimeout(_options.RequestTimeoutMilliseconds)
                .WithEncrypt(),
            predicate: p => p.Stage == GenerationReportStage.RESPONSE && p.Target == target,
            ct).ConfigureAwait(false);

        IReadOnlyDictionary<string, object?> data = GenerationReportDataParser.Parse(response.DataJson);

        return new DashboardReportSnapshot(target, response.Reason, data, DateTimeOffset.Now);
    }

    private async Task ResetSessionAsync()
    {
        _handshaken = false;
        _authorized = false;

        TcpSession? session = Interlocked.Exchange(ref _session, null);
        if (session is null)
        {
            return;
        }

        _state.Log("DEBUG", "Resetting dashboard TCP session.");
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
