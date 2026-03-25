// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Net;
using System.Runtime.CompilerServices;
using Nalix.Common.Networking;

namespace Nalix.Network.Connections;

public sealed partial class Connection
{
    /// <inheritdoc />
    [DebuggerNonUserCode]
    [SkipLocalsInit]
    [DebuggerDisplay("{ToString()}")]
    [ExcludeFromCodeCoverage]
    internal readonly struct Endpoint : INetworkEndpoint, IEquatable<Endpoint>
    {
        /// <summary>
        /// Gets an empty endpoint (no IP, no port).
        /// </summary>
        public static Endpoint Empty { get; } = new Endpoint(0, 0, 0, false, false);

        #region Fields

        private readonly ulong _hi;
        private readonly ulong _lo;
        private readonly int _port;

        #endregion Fields

        #region Factory

        /// <inheritdoc />
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static Endpoint FromIpAddress(
            IPAddress ip)
        {
            NormalizeAddress(ip, out ulong hi, out ulong lo, out bool isV6);
            return new Endpoint(hi, lo, 0, isV6, hasPort: false);
        }

        /// <inheritdoc />
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        public static Endpoint FromEndPoint(
            EndPoint? endpoint)
        {
            if (endpoint is not IPEndPoint ipEndPoint)
            {
                throw new ArgumentException("Endpoint must be of type IPEndPoint.", nameof(endpoint));
            }

            NormalizeAddress(ipEndPoint.Address, out ulong hi, out ulong lo, out bool isV6);
            return new Endpoint(hi, lo, ipEndPoint.Port, isV6, hasPort: true);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static void NormalizeAddress(
            IPAddress ip,
            out ulong hi,
            out ulong lo,
            out bool isV6)
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
                // IPv4 stored in low 32 bits of _lo, hi = 0
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

        #endregion Factory

        #region Constructor

        private Endpoint(ulong hi, ulong lo, int port, bool isV6, bool hasPort)
        {
            _hi = hi;
            _lo = lo;
            _port = port;

            IsIPv6 = isV6;
            HasPort = hasPort;
        }

        #endregion Constructor

        #region IEndpointKey implementation

        /// <inheritdoc />
        public string Address
        {
            [Pure]
            [MethodImpl(MethodImplOptions.NoInlining |
                MethodImplOptions.AggressiveOptimization)]
            get
            {
                if (!IsIPv6)
                {
                    // IPv4 stored in low 32 bits
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

        /// <inheritdoc />
        public int Port
        {
            [Pure]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => HasPort ? _port : 0;
        }

        /// <inheritdoc />
        public bool HasPort
        {
            [Pure]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <inheritdoc />
        public bool IsIPv6
        {
            [Pure]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        #endregion IEndpointKey implementation

        #region Equality & hashing

        /// <inheritdoc />
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Endpoint other)
        {
            return _hi == other._hi &&
                   _lo == other._lo &&
                   IsIPv6 == other.IsIPv6 &&
                   HasPort == other.HasPort &&
                   (!HasPort || _port == other._port);
        }

        /// <inheritdoc />
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(INetworkEndpoint? other)
        {
            if (other is null)
            {
                return false;
            }

            // Fast path for same concrete type
            return other is Endpoint concrete
                ? Equals(concrete)
                : string.Equals(
                Address,
                other.Address,
                StringComparison.Ordinal);
        }

        /// <inheritdoc />
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj) =>
            obj is Endpoint k && Equals(k);

        /// <inheritdoc />
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            int port = HasPort ? _port : 0;
            return HashCode.Combine(_hi, _lo, IsIPv6, HasPort, port);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Endpoint left, Endpoint right) => left.Equals(right);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Endpoint left, Endpoint right) => !left.Equals(right);

        #endregion Equality & hashing

        #region Formatting

        /// <inheritdoc />
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            string addr = Address;
            if (!HasPort)
            {
                return addr;
            }

            // Standard URI-style endpoint formatting.
            return !IsIPv6
                ? $"{addr}:{_port}"
                : $"[{addr}]:{_port}";
        }

        #endregion Formatting
    }
}
