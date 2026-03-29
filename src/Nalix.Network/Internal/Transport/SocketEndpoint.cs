// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Net;
using System.Runtime.CompilerServices;
using Nalix.Common.Networking;

namespace Nalix.Network.Internal.Transport;

[SkipLocalsInit]
[DebuggerNonUserCode]
[ExcludeFromCodeCoverage]
[DebuggerDisplay("{ToString()}")]
internal readonly struct SocketEndpoint : INetworkEndpoint, IEquatable<SocketEndpoint>
{
    public static SocketEndpoint Empty { get; } = new(0, 0, 0, false, false);

    private readonly ulong _hi;
    private readonly ulong _lo;
    private readonly int _port;

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static SocketEndpoint FromIpAddress(IPAddress ip)
    {
        NormalizeAddress(ip, out ulong hi, out ulong lo, out bool isV6);
        return new SocketEndpoint(hi, lo, 0, isV6, hasPort: false);
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static SocketEndpoint FromEndPoint(EndPoint? endpoint)
    {
        if (endpoint is not IPEndPoint ipEndPoint)
        {
            throw new ArgumentException("Endpoint must be of type IPEndPoint.", nameof(endpoint));
        }

        NormalizeAddress(ipEndPoint.Address, out ulong hi, out ulong lo, out bool isV6);
        return new SocketEndpoint(hi, lo, ipEndPoint.Port, isV6, hasPort: true);
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void NormalizeAddress(IPAddress ip, out ulong hi, out ulong lo, out bool isV6)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        Span<byte> buf = stackalloc byte[16];
        if (!ip.TryWriteBytes(buf, out int written))
        {
            byte[] tmp = ip.GetAddressBytes();
            MemoryExtensions.CopyTo(tmp, buf);
            written = tmp.Length;
        }

        if (written == 4)
        {
            uint v4 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(buf[..4]);
            hi = 0UL;
            lo = v4;
            isV6 = false;
        }
        else
        {
            hi = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(buf[..8]);
            lo = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(buf.Slice(8, 8));
            isV6 = true;
        }
    }

    private SocketEndpoint(ulong hi, ulong lo, int port, bool isV6, bool hasPort)
    {
        _hi = hi;
        _lo = lo;
        _port = port;

        this.IsIPv6 = isV6;
        this.HasPort = hasPort;
    }

    public string Address
    {
        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if (!this.IsIPv6)
            {
                uint v4 = (uint)_lo;
                byte[] bytes = new byte[4];
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes, v4);
                return new IPAddress(bytes).ToString();
            }

            Span<byte> buf = stackalloc byte[16];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(buf, _hi);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(buf[8..], _lo);

            return new IPAddress(buf).ToString();
        }
    }

    public int Port
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.HasPort ? _port : 0;
    }

    public bool HasPort
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    public bool IsIPv6
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(SocketEndpoint other)
    {
        return _hi == other._hi &&
               _lo == other._lo &&
               this.IsIPv6 == other.IsIPv6 &&
               this.HasPort == other.HasPort &&
               (!this.HasPort || _port == other._port);
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(INetworkEndpoint? other)
    {
        if (other is null)
        {
            return false;
        }

        return other is SocketEndpoint concrete
            ? this.Equals(concrete)
            : string.Equals(this.Address, other.Address, StringComparison.Ordinal);
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is SocketEndpoint k && this.Equals(k);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        int port = this.HasPort ? _port : 0;
        return HashCode.Combine(_hi, _lo, this.IsIPv6, this.HasPort, port);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(SocketEndpoint left, SocketEndpoint right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(SocketEndpoint left, SocketEndpoint right) => !left.Equals(right);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString()
    {
        string addr = this.Address;
        if (!this.HasPort)
        {
            return addr;
        }

        return !this.IsIPv6
            ? $"{addr}:{_port}"
            : $"[{addr}]:{_port}";
    }
}
