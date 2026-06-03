// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Configuration;
using Nalix.Traversal.Internal;
using Nalix.Traversal.Options;

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

    public void OnConnectionClosed(object? sender, IConnectEventArgs args) => _manager.RemoveSession(this.Token);

    public void Dispose() => _requester.OnCloseEvent -= this.OnConnectionClosed;
}

/// <summary>
/// Manages Reflector sessions and maps ReflectorTokens to connected endpoints.
/// </summary>
public sealed class ReflectorManager
{
    private static readonly ReflectorOptions s_options = ConfigurationManager.Instance.Get<ReflectorOptions>();

    private readonly ConcurrentDictionary<ulong, ReflectorSession> _sessions = new();

    private ulong _nextToken = 1;

    /// <summary>
    /// Creates a new Reflector Session between two peers.
    /// </summary>
    public ulong CreateSession(ulong peerA, ulong peerB, IConnection requester)
    {
        ulong token = (ulong)Interlocked.Increment(ref Unsafe.As<ulong, long>(ref _nextToken));
        _sessions[token] = new ReflectorSession(token, peerA, peerB, this, requester, s_options.BandwidthBurstCapacity, s_options.BandwidthFillRate);
        return token;
    }

    /// <summary>
    /// Tries to get the active Reflector Session by its token.
    /// </summary>
    public bool TryGetSession(ulong token, [MaybeNullWhen(false)] out ReflectorSession session) => _sessions.TryGetValue(token, out session);

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
