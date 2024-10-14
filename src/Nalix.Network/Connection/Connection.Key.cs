// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;

namespace Nalix.Network.Connection;

public sealed partial class Connection
{
    /// <inheritdoc />
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.CompilerServices.SkipLocalsInit]
    [System.Diagnostics.DebuggerDisplay("{ToString()}")]
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public readonly struct EndpointKey : IEndpointKey, System.IEquatable<EndpointKey>
    {
        private readonly System.UInt64 _hi;
        private readonly System.UInt64 _lo;
        private readonly System.Int32 _port;

        #region Factory

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public static EndpointKey FromIpAddress(
            [System.Diagnostics.CodeAnalysis.DisallowNull] System.Net.IPAddress ip)
        {
            NormalizeAddress(ip, out System.UInt64 hi, out System.UInt64 lo, out System.Boolean isV6);
            return new EndpointKey(hi, lo, 0, isV6, hasPort: false);
        }

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public static EndpointKey FromEndPoint(
            [System.Diagnostics.CodeAnalysis.AllowNull] System.Net.EndPoint endpoint)
        {
            if (endpoint is not System.Net.IPEndPoint ipEndPoint)
            {
                throw new System.ArgumentException("Endpoint must be of type IPEndPoint.", nameof(endpoint));
            }

            NormalizeAddress(ipEndPoint.Address, out System.UInt64 hi, out System.UInt64 lo, out System.Boolean isV6);
            return new EndpointKey(hi, lo, ipEndPoint.Port, isV6, hasPort: true);
        }

        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        private static void NormalizeAddress(
            [System.Diagnostics.CodeAnalysis.DisallowNull] System.Net.IPAddress ip,
            [System.Diagnostics.CodeAnalysis.DisallowNull] out System.UInt64 hi,
            [System.Diagnostics.CodeAnalysis.DisallowNull] out System.UInt64 lo,
            [System.Diagnostics.CodeAnalysis.DisallowNull] out System.Boolean isV6)
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

        #region Ctor

        private EndpointKey(System.UInt64 hi, System.UInt64 lo, System.Int32 port, System.Boolean isV6, System.Boolean hasPort)
        {
            _hi = hi;
            _lo = lo;
            _port = port;

            this.IsIPv6 = isV6;
            this.HasPort = hasPort;
        }

        #endregion

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
        public System.Boolean Equals(EndpointKey other)
        {
            return _hi == other._hi &&
                   _lo == other._lo &&
                   this.IsIPv6 == other.IsIPv6 &&
                   this.HasPort == other.HasPort &&
                   (!this.HasPort || _port == other._port);
        }

        /// <summary>
        /// Compares this instance to another <see cref="IEndpointKey"/>.
        /// </summary>
        /// <remarks>
        /// Fast path is used when <paramref name="other"/> is also a <see cref="EndpointKey"/>.
        /// Otherwise, comparison falls back to the interface properties.
        /// </remarks>
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean Equals(Nalix.Common.Abstractions.IEndpointKey? other)
        {
            if (other is null)
            {
                return false;
            }

            // Fast path for same concrete type
            if (other is EndpointKey concrete)
            {
                return Equals(concrete);
            }

            // Fallback: compare via interface contract (canonical textual representation).
            if (IsIPv6 != other.IsIPv6)
            {
                return false;
            }

            if (HasPort != other.HasPort)
            {
                return false;
            }

            if (HasPort && Port != other.Port)
            {
                return false;
            }

            return System.String.Equals(
                Address,
                other.Address,
                System.StringComparison.Ordinal);
        }

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public override System.Boolean Equals(System.Object? obj) =>
            obj is EndpointKey k && Equals(k);

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public override System.Int32 GetHashCode()
        {
            System.Int32 port = this.HasPort ? _port : 0;
            return System.HashCode.Combine(_hi, _lo, this.IsIPv6, this.HasPort, port);
        }

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static System.Boolean operator ==(EndpointKey left, EndpointKey right) => left.Equals(right);

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static System.Boolean operator !=(EndpointKey left, EndpointKey right) => !left.Equals(right);

        #endregion

        #region Formatting

        /// <inheritdoc />
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
