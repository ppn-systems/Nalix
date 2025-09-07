

namespace Nalix.Network.Internal;

internal readonly struct IpAddressKey : System.IEquatable<IpAddressKey>
{
    private readonly System.UInt64 _hi;
    private readonly System.UInt64 _lo;
    private readonly System.Boolean _isV6;

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static IpAddressKey FromIpAddress(System.Net.IPAddress ip)
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
            return new IpAddressKey(0UL, v4, false);
        }
        else
        {
            System.UInt64 hi = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(buf[..8]);
            System.UInt64 lo = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(buf.Slice(8, 8));
            return new IpAddressKey(hi, lo, true);
        }
    }
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static IpAddressKey FromEndPoint(System.Net.IPEndPoint ep) => FromIpAddress(ep.Address);

    private IpAddressKey(System.UInt64 hi, System.UInt64 lo, System.Boolean isV6)
    {
        _hi = hi;
        _lo = lo;
        _isV6 = isV6;
    }

    public override System.Int32 GetHashCode() => System.HashCode.Combine(_hi, _lo, _isV6);

    public override System.Boolean Equals(System.Object? obj) => obj is IpAddressKey k && Equals(k);

    public System.Boolean Equals(IpAddressKey other) => _hi == other._hi && _lo == other._lo && _isV6 == other._isV6;

    public static System.Boolean operator ==(IpAddressKey left, IpAddressKey right) => left.Equals(right);

    public static System.Boolean operator !=(IpAddressKey left, IpAddressKey right) => !left.Equals(right);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt64 ReverseBytes(System.UInt64 value)
    {
        return ((value & 0x00000000000000FFUL) << 56) |
               ((value & 0x000000000000FF00UL) << 40) |
               ((value & 0x0000000000FF0000UL) << 24) |
               ((value & 0x00000000FF000000UL) << 8) |
               ((value & 0x000000FF00000000UL) >> 8) |
               ((value & 0x0000FF0000000000UL) >> 24) |
               ((value & 0x00FF000000000000UL) >> 40) |
               ((value & 0xFF00000000000000UL) >> 56);
    }
}