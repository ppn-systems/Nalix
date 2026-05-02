// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Codec.DataFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.SDK.Client.Core;

/// <summary>
/// Wraps a <see cref="TcpSession"/> with packet registry setup and 
/// convenience helpers for the example client.
/// </summary>
internal sealed class ClientSession : IAsyncDisposable
{
    private readonly TcpSession _session;

    public TcpSession Session => _session;

    public bool IsConnected => _session.IsConnected;

    public ClientSession(TransportOptions options)
    {
        // PacketRegistryFactory default ctor registers built-in packets:
        // Control, Handshake, SessionResume, Directive
        PacketRegistry catalog = new PacketRegistryFactory().CreateCatalog();

        _session = new TcpSession(options, catalog);
    }

    /// <summary>Connects to the server (no handshake — example server is unencrypted).</summary>
    public async Task ConnectAsync(CancellationToken ct = default)
        => await _session.ConnectAsync(ct: ct).ConfigureAwait(false);

    /// <summary>Sends a single PING (opcode 100) and returns the RTT in ms.</summary>
    public async Task<double> PingOnceAsync(int timeoutMs = 5000, CancellationToken ct = default)
        => await _session.PingAsync(timeoutMs, ct).ConfigureAwait(false);

    /// <summary>Gracefully disconnects.</summary>
    public async Task DisconnectAsync()
        => await _session.DisconnectAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        if (_session.IsConnected)
        {
            await _session.DisconnectAsync().ConfigureAwait(false);
        }
        _session.Dispose();
    }
}
