// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Net;

[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{Address}")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal readonly struct NetAddressKey : System.IEquatable<NetAddressKey>
{
    private readonly System.UInt64 _hi;
    private readonly System.UInt64 _lo;
    private readonly System.Boolean _isV6;

    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static NetAddressKey FromIpAddress([System.Diagnostics.CodeAnalysis.DisallowNull] System.Net.IPAddress ip)
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
            System.UInt32 v4 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(buf[..4]);
            return new NetAddressKey(0UL, v4, false);
        }
        else
        {
            System.UInt64 hi = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(buf[..8]);
            System.UInt64 lo = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(buf.Slice(8, 8));
            return new NetAddressKey(hi, lo, true);
        }
    }

    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static NetAddressKey FromEndPoint([System.Diagnostics.CodeAnalysis.DisallowNull] System.Net.IPEndPoint ep) => FromIpAddress(ep.Address);

    private NetAddressKey(System.UInt64 hi, System.UInt64 lo, System.Boolean isV6)
    {
        _hi = hi;
        _lo = lo;
        _isV6 = isV6;
    }

    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.Int32 GetHashCode() => System.HashCode.Combine(_hi, _lo, _isV6);

    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.Boolean Equals(System.Object? obj) => obj is NetAddressKey k && Equals(k);

    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean Equals(NetAddressKey other) => _hi == other._hi && _lo == other._lo && _isV6 == other._isV6;

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean operator ==(NetAddressKey left, NetAddressKey right) => left.Equals(right);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean operator !=(NetAddressKey left, NetAddressKey right) => !left.Equals(right);

    public System.String Address
    {
        [System.Diagnostics.Contracts.Pure]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        get
        {
            if (!_isV6)
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

    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.String ToString() => this.Address;
}