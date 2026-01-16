// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Core.Abstractions;

namespace Nalix.Network.Connections;

public sealed partial class Connection
{
    /// <inheritdoc />
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.CompilerServices.SkipLocalsInit]
    [System.Diagnostics.DebuggerDisplay("{ToString()}")]
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal readonly struct NetworkEndpoint : INetworkEndpoint, System.IEquatable<NetworkEndpoint>
    {
        #region Fields

        private readonly System.UInt64 _hi;
        private readonly System.UInt64 _lo;
        private readonly System.Int32 _port;

        #endregion Fields

        #region Factory

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public static NetworkEndpoint FromIpAddress(
            [System.Diagnostics.CodeAnalysis.NotNull] System.Net.IPAddress ip)
        {
            NormalizeAddress(ip, out System.UInt64 hi, out System.UInt64 lo, out System.Boolean isV6);
            return new NetworkEndpoint(hi, lo, 0, isV6, hasPort: false);
        }

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public static NetworkEndpoint FromEndPoint(
            [System.Diagnostics.CodeAnalysis.AllowNull] System.Net.EndPoint endpoint)
        {
            if (endpoint is not System.Net.IPEndPoint ipEndPoint)
            {
                throw new System.ArgumentException("Endpoint must be of type IPEndPoint.", nameof(endpoint));
            }

            NormalizeAddress(ipEndPoint.Address, out System.UInt64 hi, out System.UInt64 lo, out System.Boolean isV6);
            return new NetworkEndpoint(hi, lo, ipEndPoint.Port, isV6, hasPort: true);
        }

        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        private static void NormalizeAddress(
            [System.Diagnostics.CodeAnalysis.NotNull] System.Net.IPAddress ip,
            [System.Diagnostics.CodeAnalysis.NotNull] out System.UInt64 hi,
            [System.Diagnostics.CodeAnalysis.NotNull] out System.UInt64 lo,
            [System.Diagnostics.CodeAnalysis.NotNull] out System.Boolean isV6)
        {
            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }

            System.Span<System.Byte> buf = stackalloc System.Byte[16];
            if (!ip.TryWriteBytes(buf, out System.Int32 written))
            {
                System.Byte[] tmp = ip.GetAddressBytes();
                System.MemoryExtensions.CopyTo(tmp, buf);
                written = tmp.Length;
            }

            if (written == 4)
            {
                // IPv4 stored in low 32 bits of _lo, hi = 0
                System.UInt32 v4 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(buf[..4]);
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

        #endregion

        #region Constructor

        private NetworkEndpoint(System.UInt64 hi, System.UInt64 lo, System.Int32 port, System.Boolean isV6, System.Boolean hasPort)
        {
            _hi = hi;
            _lo = lo;
            _port = port;

            this.IsIPv6 = isV6;
            this.HasPort = hasPort;
        }

        #endregion Constructor

        #region IEndpointKey implementation

        /// <inheritdoc />
        public System.String Address
        {
            [System.Diagnostics.Contracts.Pure]
            [System.Runtime.CompilerServices.MethodImpl(
                System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
                System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
            get
            {
                if (!this.IsIPv6)
                {
                    // IPv4 stored in low 32 bits
                    System.UInt32 v4 = (System.UInt32)_lo;
                    System.Byte[] bytes = new System.Byte[4];
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes, v4);
                    return new System.Net.IPAddress(bytes).ToString();
                }
                else
                {
                    System.Span<System.Byte> buf = stackalloc System.Byte[16];
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(buf, _hi);
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(buf[8..], _lo);
                    return new System.Net.IPAddress(buf).ToString();
                }
            }
        }

        /// <inheritdoc />
        public System.Int32 Port
        {
            [System.Diagnostics.Contracts.Pure]
            [System.Runtime.CompilerServices.MethodImpl(
                System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get => this.HasPort ? _port : 0;
        }

        /// <inheritdoc />
        public System.Boolean HasPort
        {
            [System.Diagnostics.Contracts.Pure]
            [System.Runtime.CompilerServices.MethodImpl(
                System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <inheritdoc />
        public System.Boolean IsIPv6
        {
            [System.Diagnostics.Contracts.Pure]
            [System.Runtime.CompilerServices.MethodImpl(
                System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
        }

        #endregion

        #region Equality & hashing

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public System.Boolean Equals(NetworkEndpoint other)
        {
            return _hi == other._hi &&
                   _lo == other._lo &&
                   this.IsIPv6 == other.IsIPv6 &&
                   this.HasPort == other.HasPort &&
                   (!this.HasPort || _port == other._port);
        }

        /// <summary>
        /// Compares this instance to another <see cref="INetworkEndpoint"/>.
        /// </summary>
        /// <remarks>
        /// Fast path is used when <paramref name="other"/> is also a <see cref="NetworkEndpoint"/>.
        /// Otherwise, comparison falls back to the interface properties.
        /// </remarks>
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public System.Boolean Equals([System.Diagnostics.CodeAnalysis.AllowNull] INetworkEndpoint other)
        {
            if (other is null)
            {
                return false;
            }

            // Fast path for same concrete type
            return other is NetworkEndpoint concrete
                ? Equals(concrete)
                : System.String.Equals(
                Address,
                other.Address,
                System.StringComparison.Ordinal);
        }

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public override System.Boolean Equals([System.Diagnostics.CodeAnalysis.AllowNull] System.Object obj) =>
            obj is NetworkEndpoint k && Equals(k);

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public override System.Int32 GetHashCode()
        {
            System.Int32 port = this.HasPort ? _port : 0;
            return System.HashCode.Combine(_hi, _lo, this.IsIPv6, this.HasPort, port);
        }

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public static System.Boolean operator ==(NetworkEndpoint left, NetworkEndpoint right) => left.Equals(right);

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public static System.Boolean operator !=(NetworkEndpoint left, NetworkEndpoint right) => !left.Equals(right);

        #endregion

        #region Formatting

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public override System.String ToString()
        {
            System.String addr = Address;
            if (!this.HasPort)
            {
                return addr;
            }

            // Standard URI-style endpoint formatting.
            return !this.IsIPv6
                ? $"{addr}:{_port}"
                : $"[{addr}]:{_port}";
        }

        #endregion
    }
}
