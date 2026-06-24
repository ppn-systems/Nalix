// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.SDK.Options;
using Nalix.SDK.Transport.Extensions;

#pragma warning disable CA2012 // ValueTask is properly awaited by the caller
#pragma warning disable NALIX076 // Acceptable in synchronous builder pattern before await

namespace Nalix.SDK.Transport.Rpc;

/// <summary>
/// Represents an awaitable RPC call that expects a single response packet.
/// </summary>
/// <typeparam name="TResponse">The expected response packet type.</typeparam>
public readonly struct RpcCall<TResponse> where TResponse : class, IPacket, IPacketStaticOpcode
{
    private readonly TransportSession _session;
    private readonly IPacket _request;

    private readonly RequestOptions? _options;

    /// <inheritdoc/>
    public RpcCall(TransportSession session, IPacket request, RequestOptions? options = null)
    {
        _session = session;
        _request = request;
        _options = options;
    }

    /// <summary>
    /// Executes the RPC call and returns the response.
    /// </summary>
    public ValueTask<TResponse> GetResponseAsync()
        => _session.RequestAsync<TResponse>(_request, _options);

    /// <summary>
    /// Gets an awaiter used to await this RPC call.
    /// </summary>
    public ValueTaskAwaiter<TResponse> GetAwaiter() => this.GetResponseAsync().GetAwaiter();
}
