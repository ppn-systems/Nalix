// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Chat.Client.Core.Networking;

namespace Nalix.Chat.Client.Core.Services;

/// <summary>
/// Collects diagnostics for the client diagnostics sidebar.
/// </summary>
public sealed class DiagnosticsService(INetworkClient networkClient)
{
    private readonly INetworkClient _networkClient = networkClient ?? throw new ArgumentNullException(nameof(networkClient));

    /// <summary>
    /// Collects a fresh diagnostics snapshot.
    /// </summary>
    public async ValueTask<DiagnosticsSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        double rttMs = 0;
        double driftMs = 0;

        if (_networkClient.ConnectionState == ConnectionState.Connected)
        {
            rttMs = await _networkClient.PingAsync(cancellationToken).ConfigureAwait(false);
            driftMs = await _networkClient.SyncClockAsync(cancellationToken).ConfigureAwait(false);
        }

        return new DiagnosticsSnapshot(
            rttMs,
            driftMs,
            _networkClient.ActiveCipher,
            _networkClient.CipherRotationCounter,
            _networkClient.SessionIdentifier,
            _networkClient.ConnectionState);
    }
}

/// <summary>
/// Immutable diagnostics projection for the UI.
/// </summary>
public readonly record struct DiagnosticsSnapshot(
    double RttMs,
    double DriftMs,
    Nalix.Common.Security.CipherSuiteType ActiveCipher,
    int CipherRotationCounter,
    string SessionIdentifier,
    ConnectionState ConnectionState);
