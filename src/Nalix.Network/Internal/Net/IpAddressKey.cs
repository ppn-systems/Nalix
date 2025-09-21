// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Net;

internal readonly struct IPAddressKey : System.IEquatable<IPAddressKey>
{
    private readonly System.UInt64 _hi;
    private readonly System.UInt64 _lo;
    private readonly System.Boolean _isV6;

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static IPAddressKey FromIpAddress(System.Net.IPAddress ip)
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
            return new IPAddressKey(0UL, v4, false);
        }
        else
        {
            System.UInt64 hi = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(buf[..8]);
            System.UInt64 lo = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(buf.Slice(8, 8));
            return new IPAddressKey(hi, lo, true);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static IPAddressKey FromEndPoint(System.Net.IPEndPoint ep) => FromIpAddress(ep.Address);

    private IPAddressKey(System.UInt64 hi, System.UInt64 lo, System.Boolean isV6)
    {
        _hi = hi;
        _lo = lo;
        _isV6 = isV6;
    }

    public override System.Int32 GetHashCode() => System.HashCode.Combine(_hi, _lo, _isV6);

    public override System.Boolean Equals(System.Object? obj) => obj is IPAddressKey k && Equals(k);

    public System.Boolean Equals(IPAddressKey other) => _hi == other._hi && _lo == other._lo && _isV6 == other._isV6;

    public static System.Boolean operator ==(IPAddressKey left, IPAddressKey right) => left.Equals(right);

    public static System.Boolean operator !=(IPAddressKey left, IPAddressKey right) => !left.Equals(right);

    public System.String Address
    {
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

    public override System.String ToString() => Address;
}