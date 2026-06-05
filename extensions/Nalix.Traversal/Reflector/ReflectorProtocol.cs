// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Protocols;

namespace Nalix.Traversal.Reflector;

/// <summary>
/// A specialized protocol to handle UdpPassthroughListener for Reflector.
/// It implements both IProtocol and IFrameProcessor to minimize overhead.
/// </summary>
public sealed class ReflectorProtocol : IProtocol, IFrameProcessor, IOpCodeExtractor
{
    private readonly ReflectorManager _manager;

    public static string Name => "Nalix.Traversal.Reflector";

    public bool KeepConnectionOpen => true;
    public IFrameProcessor FrameProcessor => this;
    public IOpCodeExtractor OpCodeExtractor => this;

    public ReflectorProtocol(ReflectorManager manager) => _manager = manager ?? throw new ArgumentNullException(nameof(manager));

    /// <inheritdoc/>
    public void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        // Not used, handled in ProcessFrame
    }

    /// <inheritdoc/>
    public void PostProcessMessage(object? sender, IConnectEventArgs args)
    {
        // Not used
    }

    /// <inheritdoc/>
    public void OnAccept(IConnection connection, System.Threading.CancellationToken cancellationToken = default)
    {
        // Passthrough listener doesn't trigger OnAccept
    }

    /// <inheritdoc/>
    public void ProcessFrame(object? sender, IConnectEventArgs args)
    {
        if (args?.Lease is not { } lease)
        {
            return;
        }

        try
        {
            // The packet payload is the Raw payload.
            ReadOnlySpan<byte> buffer = lease.Memory.Span;

            if (buffer.Length < 8)
            {
                return; // Invalid Reflector packet
            }

            // Offset 0: ReflectorToken (ulong)
            ulong token = BinaryPrimitives.ReadUInt64LittleEndian(buffer);

            if (!_manager.TryGetSession(token, out ReflectorSession? session))
            {
                return; // Invalid or expired session
            }

            IConnection? senderConnection = args.Connection;
            IConnection? targetConnection;

            // To support both directions, we first learn the endpoint from the sender.
            // Identify sender and get target
            // NOTE: In a real system, we'd need to verify sender's identity,
            // but since ReflectorToken is unguessable (e.g. 64-bit random), possessing the token is sufficient.
            if (session.PeerAConnection == null ||
                (session.PeerAConnection.NetworkEndpoint.Address == senderConnection.NetworkEndpoint.Address &&
                 session.PeerAConnection.NetworkEndpoint.Port == senderConnection.NetworkEndpoint.Port))
            {
                // Sender is likely PeerA. Update its connection.
                session.PeerAConnection = senderConnection;
                targetConnection = session.PeerBConnection;
            }
            else if (session.PeerBConnection == null ||
                     (session.PeerBConnection.NetworkEndpoint.Address == senderConnection.NetworkEndpoint.Address &&
                      session.PeerBConnection.NetworkEndpoint.Port == senderConnection.NetworkEndpoint.Port))
            {
                // Sender is likely PeerB. Update its connection.
                session.PeerBConnection = senderConnection;
                targetConnection = session.PeerAConnection;
            }
            else
            {
                // Token leaked to a 3rd party? Drop.
                return;
            }

            // Enforce Bandwidth Rate Limiting
            // The buffer length represents the size of the UDP packet.
            if (!session.Bucket.TryConsume(buffer.Length))
            {
                // Rate limit exceeded (e.g., > 200 KB/s). Drop the packet.
                return;
            }

            // Forward the EXACT SAME PACKET to the target.
            targetConnection?.UDP.Send(buffer);
        }
        finally
        {
            lease.Dispose();
            args.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // No unmanaged resources to release
    }

    /// <inheritdoc/>
    public string GenerateReport() => string.Empty;

    /// <inheritdoc/>
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        // Not used
    }

    // Explicit implementation for IOpCodeExtractor property
    ushort IOpCodeExtractor.Extract(ReadOnlySpan<byte> buffer) => 0;
}
