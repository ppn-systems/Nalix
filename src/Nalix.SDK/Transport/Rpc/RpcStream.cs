// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using Nalix.Abstractions.Networking.Packets;
using Nalix.SDK.Options;
using Nalix.SDK.Transport.Extensions;

#pragma warning disable CA1711 // Conceptually an RPC stream, not an IO stream
#pragma warning disable NALIX076 // Acceptable in synchronous builder pattern before await

namespace Nalix.SDK.Transport.Rpc;

/// <summary>
/// Represents an awaitable RPC call that expects a stream of response packets.
/// </summary>
/// <typeparam name="TResponse">The expected response packet type.</typeparam>
public readonly struct RpcStream<TResponse> where TResponse : class, IPacket, IPacketStaticOpcode, IPacketStreamable
{
    private readonly TransportSession _session;
    private readonly IPacket _request;

    private readonly RequestOptions? _options;

    /// <inheritdoc/>
    public RpcStream(TransportSession session, IPacket request, RequestOptions? options = null)
    {
        _session = session;
        _request = request;
        _options = options;
    }

    /// <summary>
    /// Executes the RPC call and returns an asynchronous stream of responses.
    /// </summary>
    public IAsyncEnumerable<TResponse> GetStreamAsync()
        => _session.StreamAsync<TResponse>(_request, _options);

    /// <summary>
    /// Gets an enumerator used to iterate this RPC stream.
    /// </summary>
    public IAsyncEnumerator<TResponse> GetAsyncEnumerator() => this.GetStreamAsync().GetAsyncEnumerator();
}
