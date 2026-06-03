// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Nalix.Abstractions.Networking;
using Nalix.Traversal.Internal;

namespace Nalix.Traversal.Reflector;

/// <summary>
/// Represents an active Reflector Session between two peers.
/// </summary>
public sealed class ReflectorSession
{
    public ulong Token { get; }
    public ulong PeerAId { get; }
    public ulong PeerBId { get; }

    public IConnection? PeerAConnection { get; set; }
    public IConnection? PeerBConnection { get; set; }

    /// <summary>
    /// Byte-level bandwidth limiter for this Reflector session.
    /// Default: 500 KB/s burst, 200 KB/s fill rate.
    /// </summary>
    internal TokenBucket Bucket { get; }

    private readonly IConnection _requester;
    private readonly ReflectorManager _manager;

    public ReflectorSession(ulong token, ulong peerAId, ulong peerBId, ReflectorManager manager, IConnection requester, long capacity, long fillRate)
    {
        this.Token = token;
        this.PeerAId = peerAId;
        this.PeerBId = peerBId;

        _requester = requester;
        _manager = manager;
        this.Bucket = new TokenBucket(capacity, fillRate);

        // Hook up event to clean up session when the TCP connection dies
        _requester.OnCloseEvent += this.OnConnectionClosed;
    }

    public void OnConnectionClosed(object? sender, Nalix.Abstractions.Networking.IConnectEventArgs args) => _manager.RemoveSession(this.Token);

    public void Dispose()
    {
        // Prevent event leaks by explicitly unhooking from the requester connection
        _requester.OnCloseEvent -= this.OnConnectionClosed;
    }
}

/// <summary>
/// Manages Reflector sessions and maps ReflectorTokens to connected endpoints.
/// </summary>
public sealed class ReflectorManager
{
    private readonly ConcurrentDictionary<ulong, ReflectorSession> _sessions = new();
    private readonly Nalix.Traversal.Options.ReflectorOptions _options;
    private ulong _nextToken = 1;

    public ReflectorManager() => _options = Environment.Configuration.ConfigurationManager.Instance.Get<Nalix.Traversal.Options.ReflectorOptions>();

    /// <summary>
    /// Creates a new Reflector Session between two peers.
    /// </summary>
    public ulong CreateSession(ulong peerA, ulong peerB, Nalix.Abstractions.Networking.IConnection requester)
    {
        ulong token = (ulong)System.Threading.Interlocked.Increment(ref System.Runtime.CompilerServices.Unsafe.As<ulong, long>(ref _nextToken));
        _sessions[token] = new ReflectorSession(token, peerA, peerB, this, requester, _options.BandwidthBurstCapacity, _options.BandwidthFillRate);
        return token;
    }

    /// <summary>
    /// Tries to get the active Reflector Session by its token.
    /// </summary>
    public bool TryGetSession(ulong token, [MaybeNullWhen(false)] out ReflectorSession session) => _sessions.TryGetValue(token, out session);

    /// <summary>
    /// Updates the connection for a peer in the Reflector session.
    /// </summary>
    public void UpdateConnection(ulong token, ulong peerId, Nalix.Abstractions.Networking.IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (_sessions.TryGetValue(token, out ReflectorSession? session))
        {
            if (session.PeerAId == peerId)
            {
                session.PeerAConnection = connection;
            }
            else if (session.PeerBId == peerId)
            {
                session.PeerBConnection = connection;
            }
        }
    }

    /// <summary>
    /// Removes a Reflector Session.
    /// </summary>
    public void RemoveSession(ulong token)
    {
        if (_sessions.TryRemove(token, out ReflectorSession? session))
        {
            session.Dispose();
        }
    }
}
