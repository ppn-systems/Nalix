// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking;

namespace Nalix.Network.Connections;

public sealed partial class Connection
{
    /// <inheritdoc />
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.CompilerServices.SkipLocalsInit]
    [System.Diagnostics.DebuggerDisplay("{ToString()}")]
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal readonly struct Endpoint : INetworkEndpoint, System.IEquatable<Endpoint>
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
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public static Endpoint FromIpAddress(
            [System.Diagnostics.CodeAnalysis.NotNull] System.Net.IPAddress ip)
        {
            NormalizeAddress(ip, out ulong hi, out ulong lo, out bool isV6);
            return new Endpoint(hi, lo, 0, isV6, hasPort: false);
        }

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public static Endpoint FromEndPoint(
            [System.Diagnostics.CodeAnalysis.AllowNull] System.Net.EndPoint endpoint)
        {
            if (endpoint is not System.Net.IPEndPoint ipEndPoint)
            {
                throw new System.ArgumentException("Endpoint must be of type IPEndPoint.", nameof(endpoint));
            }

            NormalizeAddress(ipEndPoint.Address, out ulong hi, out ulong lo, out bool isV6);
            return new Endpoint(hi, lo, ipEndPoint.Port, isV6, hasPort: true);
        }

        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        private static void NormalizeAddress(
            [System.Diagnostics.CodeAnalysis.NotNull] System.Net.IPAddress ip,
            [System.Diagnostics.CodeAnalysis.NotNull] out ulong hi,
            [System.Diagnostics.CodeAnalysis.NotNull] out ulong lo,
            [System.Diagnostics.CodeAnalysis.NotNull] out bool isV6)
        {
            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }

            System.Span<byte> buf = stackalloc byte[16];
            if (!ip.TryWriteBytes(buf, out int written))
            {
                byte[] tmp = ip.GetAddressBytes();
                System.MemoryExtensions.CopyTo(tmp, buf);
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
            [System.Diagnostics.Contracts.Pure]
            [System.Runtime.CompilerServices.MethodImpl(
                System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
                System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
            get
            {
                if (!IsIPv6)
                {
                    // IPv4 stored in low 32 bits
                    uint v4 = (uint)_lo;
                    byte[] bytes = new byte[4];
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes, v4);
                    return new System.Net.IPAddress(bytes).ToString();
                }
                System.Span<byte> buf = stackalloc byte[16];
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(buf, _hi);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(buf[8..], _lo);

                return new System.Net.IPAddress(buf).ToString();
            }
        }

        /// <inheritdoc />
        public int Port
        {
            [System.Diagnostics.Contracts.Pure]
            [System.Runtime.CompilerServices.MethodImpl(
                System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get => HasPort ? _port : 0;
        }

        /// <inheritdoc />
        public bool HasPort
        {
            [System.Diagnostics.Contracts.Pure]
            [System.Runtime.CompilerServices.MethodImpl(
                System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <inheritdoc />
        public bool IsIPv6
        {
            [System.Diagnostics.Contracts.Pure]
            [System.Runtime.CompilerServices.MethodImpl(
                System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
        }

        #endregion IEndpointKey implementation

        #region Equality & hashing

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public bool Equals(Endpoint other)
        {
            return _hi == other._hi &&
                   _lo == other._lo &&
                   IsIPv6 == other.IsIPv6 &&
                   HasPort == other.HasPort &&
                   (!HasPort || _port == other._port);
        }

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public bool Equals([System.Diagnostics.CodeAnalysis.AllowNull] INetworkEndpoint other)
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
                System.StringComparison.Ordinal);
        }

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public override bool Equals([System.Diagnostics.CodeAnalysis.AllowNull] object obj) =>
            obj is Endpoint k && Equals(k);

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public override int GetHashCode()
        {
            int port = HasPort ? _port : 0;
            return System.HashCode.Combine(_hi, _lo, IsIPv6, HasPort, port);
        }

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public static bool operator ==(Endpoint left, Endpoint right) => left.Equals(right);

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public static bool operator !=(Endpoint left, Endpoint right) => !left.Equals(right);

        #endregion Equality & hashing

        #region Formatting

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
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
