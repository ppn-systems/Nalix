// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Net;
using Nalix.Network.Internal.Transport;

namespace Nalix.Network.Internal.Protocol;

internal static class ProxyProtocolParser
{
    private static ReadOnlySpan<byte> V1Magic => "PROXY "u8;
    private static ReadOnlySpan<byte> V2Magic => "\r\n\r\n\0\r\nQUIT\n"u8;

    // Maximum V1 header is 108 bytes according to HAProxy specification — used to cap stackalloc
    private const int V1MaxLength = 108;

    /// <summary>
    /// Zero-allocation overload that returns a <see cref="SocketEndpoint"/> instead of
    /// <see cref="IPEndPoint"/>. Avoids heap allocations for IPAddress and IPEndPoint
    /// on the reject path. Use this overload when the parsed endpoint may be rejected
    /// by ConnectionGuard before a Connection is created.
    /// </summary>
    /// <returns>
    /// true = header parsed OK. endpoint is SocketEndpoint.Empty if LOCAL/UNKNOWN (pass-through).
    /// false = buffer too short or incorrect format.
    /// </returns>
    public static bool TryParse(ReadOnlySpan<byte> buffer, out SocketEndpoint endpoint, out int bytesToConsume)
    {
        endpoint = SocketEndpoint.Empty;
        bytesToConsume = 0;

        if (buffer.Length < 8)
        {
            return false;
        }

        if (buffer.StartsWith(V2Magic))
        {
            return TryParseV2ZeroAlloc(buffer, out endpoint, out bytesToConsume);
        }

        if (buffer.StartsWith(V1Magic))
        {
            return TryParseV1ZeroAlloc(buffer, out endpoint, out bytesToConsume);
        }

        return false;
    }

    // ── V2 zero-alloc ─────────────────────────────────────────────────────────
    private static bool TryParseV2ZeroAlloc(ReadOnlySpan<byte> buffer, out SocketEndpoint endpoint, out int bytesToConsume)
    {
        endpoint = SocketEndpoint.Empty;
        bytesToConsume = 0;

        if (buffer.Length < 16)
        {
            return false;
        }

        byte versionCmd = buffer[12];
        byte family = buffer[13];
        int addrLen = BinaryPrimitives.ReadUInt16BigEndian(buffer[14..16]);

        if ((versionCmd >> 4) != 2)
        {
            return false;
        }

        bytesToConsume = 16 + addrLen;
        if (buffer.Length < bytesToConsume)
        {
            return false;
        }

        byte command = (byte)(versionCmd & 0x0F);
        if (command == 0x00)
        {
            return true; // LOCAL: pass-through, endpoint stays Empty
        }

        if (command != 0x01)
        {
            return false;
        }

        byte af = (byte)(family >> 4);

        switch (af)
        {
            case 0x01: // AF_INET (IPv4)
                if (addrLen < 12)
                {
                    return false;
                }
                endpoint = SocketEndpoint.FromRawBytes(buffer.Slice(16, 4), BinaryPrimitives.ReadUInt16BigEndian(buffer[24..26]), isIPv6: false);
                return true;

            case 0x02: // AF_INET6
                if (addrLen < 36)
                {
                    return false;
                }
                endpoint = SocketEndpoint.FromRawBytes(buffer.Slice(16, 16), BinaryPrimitives.ReadUInt16BigEndian(buffer[48..50]), isIPv6: true);
                return true;

            case 0x03: // AF_UNIX — pass-through
                return true;

            default:
                return false;
        }
    }

    // ── V1 zero-alloc ─────────────────────────────────────────────────────────
    private static bool TryParseV1ZeroAlloc(ReadOnlySpan<byte> buffer, out SocketEndpoint endpoint, out int bytesToConsume)
    {
        // The receive buffer may contain both the PROXY header and the first payload bytes.
        // Only the header line itself is capped by V1MaxLength.
        ReadOnlySpan<byte> searchWindow = buffer.Length > V1MaxLength
            ? buffer[..V1MaxLength]
            : buffer;

        int crlf = searchWindow.IndexOf("\r\n"u8);
        if (crlf < 0)
        {
            endpoint = default;
            bytesToConsume = 0;
            return false;
        }

        bytesToConsume = crlf + 2;

        ReadOnlySpan<byte> line = buffer[..crlf];

        if (!TryReadAsciiToken(ref line, out ReadOnlySpan<byte> signature) ||
            !signature.SequenceEqual("PROXY"u8))
        {
            endpoint = default;
            return false;
        }

        if (!TryReadAsciiToken(ref line, out ReadOnlySpan<byte> proto))
        {
            endpoint = default;
            return false;
        }

        if (proto.SequenceEqual("UNKNOWN"u8))
        {
            endpoint = default;
            return true; // Pass-through. Keep endpoint as Empty.
        }

        bool isTcp4 = proto.SequenceEqual("TCP4"u8);
        bool isTcp6 = proto.SequenceEqual("TCP6"u8);

        if (!isTcp4 && !isTcp6)
        {
            endpoint = default;
            return false;
        }

        if (!TryReadAsciiToken(ref line, out ReadOnlySpan<byte> srcIp) ||
            !TryReadAsciiToken(ref line, out ReadOnlySpan<byte> dstIp) ||
            !TryReadAsciiToken(ref line, out ReadOnlySpan<byte> srcPortSpan) ||
            !TryReadAsciiToken(ref line, out ReadOnlySpan<byte> dstPortSpan))
        {
            endpoint = default;
            return false;
        }

        // Strict V1 parser: no extra tokens after destination port.
        if (!line.IsEmpty)
        {
            endpoint = default;
            return false;
        }

        if (!TryParseUInt16Ascii(srcPortSpan, out ushort srcPort) ||
            !TryParseUInt16Ascii(dstPortSpan, out _))
        {
            endpoint = default;
            return false;
        }

        if (isTcp4)
        {
            Span<byte> srcAddress = stackalloc byte[4];
            Span<byte> dstAddress = stackalloc byte[4];

            if (!TryParseIPv4ToBytes(srcIp, srcAddress) ||
                !TryParseIPv4ToBytes(dstIp, dstAddress))
            {
                endpoint = default;
                return false;
            }

            endpoint = SocketEndpoint.FromRawBytes(srcAddress, srcPort, isIPv6: false);
            return true;
        }

        {
            Span<byte> srcAddress = stackalloc byte[16];
            Span<byte> dstAddress = stackalloc byte[16];

            if (!TryParseIPv6ToBytes(srcIp, srcAddress) ||
                !TryParseIPv6ToBytes(dstIp, dstAddress))
            {
                endpoint = default;
                return false;
            }

            endpoint = SocketEndpoint.FromRawBytes(srcAddress, srcPort, isIPv6: true);
            return true;
        }
    }

    /// <summary>
    /// Parses an IPv4 address string "d.d.d.d" directly into 4 bytes without allocating.
    /// </summary>
    private static bool TryParseIPv4ToBytes(ReadOnlySpan<char> ip, Span<byte> bytes)
    {
        if (ip.Length < 7 || ip.Length > 15)
        {
            return false;
        }

        int pos = 0;
        for (int octet = 0; octet < 4; octet++)
        {
            if (pos >= ip.Length)
            {
                return false;
            }

            int value = 0;
            int digits = 0;
            while (pos < ip.Length && ip[pos] >= '0' && ip[pos] <= '9')
            {
                value = (value * 10) + (ip[pos] - '0');
                digits++;
                pos++;
            }

            if (digits == 0 || value > 255)
            {
                return false;
            }

            bytes[octet] = (byte)value;

            if (octet < 3)
            {
                if (pos >= ip.Length || ip[pos] != '.')
                {
                    return false;
                }
                pos++;
            }
        }

        return pos == ip.Length;
    }

    private static bool TryReadAsciiToken(ref ReadOnlySpan<byte> source, out ReadOnlySpan<byte> token)
    {
        token = default;

        if (source.IsEmpty)
        {
            return false;
        }

        int space = source.IndexOf((byte)' ');
        if (space < 0)
        {
            token = source;
            source = ReadOnlySpan<byte>.Empty;
            return !token.IsEmpty;
        }

        if (space == 0)
        {
            return false;
        }

        token = source[..space];
        source = source[(space + 1)..];
        return true;
    }

    private static bool TryParseUInt16Ascii(ReadOnlySpan<byte> source, out ushort value)
    {
        value = 0;

        if (source.IsEmpty || source.Length > 5)
        {
            return false;
        }

        uint result = 0;

        for (int i = 0; i < source.Length; i++)
        {
            byte c = source[i];

            if (c < (byte)'0' || c > (byte)'9')
            {
                return false;
            }

            result = (result * 10u) + (uint)(c - (byte)'0');

            if (result > ushort.MaxValue)
            {
                return false;
            }
        }

        value = (ushort)result;
        return true;
    }

    private static bool TryParseIPv4ToBytes(ReadOnlySpan<byte> ip, Span<byte> bytes)
    {
        if (bytes.Length < 4 || ip.Length < 7 || ip.Length > 15)
        {
            return false;
        }

        int pos = 0;

        for (int octet = 0; octet < 4; octet++)
        {
            if (pos >= ip.Length)
            {
                return false;
            }

            int value = 0;
            int digits = 0;

            while (pos < ip.Length && ip[pos] >= (byte)'0' && ip[pos] <= (byte)'9')
            {
                value = (value * 10) + (ip[pos] - (byte)'0');
                digits++;
                pos++;

                if (value > 255)
                {
                    return false;
                }
            }

            if (digits == 0)
            {
                return false;
            }

            bytes[octet] = (byte)value;

            if (octet < 3)
            {
                if (pos >= ip.Length || ip[pos] != (byte)'.')
                {
                    return false;
                }

                pos++;
            }
        }

        return pos == ip.Length;
    }

    private static bool TryParseIPv6ToBytes(ReadOnlySpan<byte> ip, Span<byte> bytes)
    {
        if (bytes.Length < 16 || ip.IsEmpty)
        {
            return false;
        }

        bytes[..16].Clear();

        Span<ushort> words = stackalloc ushort[8];

        int wordCount = 0;
        int compressIndex = -1;
        int pos = 0;

        if (ip.Length >= 2 && ip[0] == (byte)':' && ip[1] == (byte)':')
        {
            compressIndex = 0;
            pos = 2;

            if (pos == ip.Length)
            {
                return WriteIPv6Words(words, wordCount, compressIndex, bytes);
            }
        }
        else if (ip[0] == (byte)':')
        {
            return false;
        }

        while (pos < ip.Length)
        {
            if (wordCount >= 8)
            {
                return false;
            }

            int nextColon = ip[pos..].IndexOf((byte)':');
            int segmentEnd = nextColon < 0 ? ip.Length : pos + nextColon;
            ReadOnlySpan<byte> segment = ip[pos..segmentEnd];

            if (segment.IsEmpty)
            {
                return false;
            }

            if (segment.Contains((byte)'.'))
            {
                if (segmentEnd != ip.Length || wordCount > 6)
                {
                    return false;
                }

                Span<byte> ipv4 = stackalloc byte[4];
                if (!TryParseIPv4ToBytes(segment, ipv4))
                {
                    return false;
                }

                words[wordCount++] = (ushort)((ipv4[0] << 8) | ipv4[1]);
                words[wordCount++] = (ushort)((ipv4[2] << 8) | ipv4[3]);
                pos = ip.Length;
                break;
            }

            if (!TryParseIPv6Word(segment, out ushort word))
            {
                return false;
            }

            words[wordCount++] = word;
            pos = segmentEnd;

            if (pos >= ip.Length)
            {
                break;
            }

            if (ip[pos] != (byte)':')
            {
                return false;
            }

            if (pos + 1 < ip.Length && ip[pos + 1] == (byte)':')
            {
                if (compressIndex >= 0)
                {
                    return false;
                }

                compressIndex = wordCount;
                pos += 2;

                if (pos == ip.Length)
                {
                    break;
                }

                continue;
            }

            pos++;

            if (pos == ip.Length)
            {
                return false;
            }
        }

        return WriteIPv6Words(words, wordCount, compressIndex, bytes);
    }

    private static bool TryParseIPv6Word(ReadOnlySpan<byte> source, out ushort value)
    {
        value = 0;

        if (source.IsEmpty || source.Length > 4)
        {
            return false;
        }

        ushort result = 0;

        for (int i = 0; i < source.Length; i++)
        {
            byte c = source[i];
            int digit;

            if (c >= (byte)'0' && c <= (byte)'9')
            {
                digit = c - (byte)'0';
            }
            else if (c >= (byte)'A' && c <= (byte)'F')
            {
                digit = c - (byte)'A' + 10;
            }
            else if (c >= (byte)'a' && c <= (byte)'f')
            {
                digit = c - (byte)'a' + 10;
            }
            else
            {
                return false;
            }

            result = (ushort)((result << 4) | digit);
        }

        value = result;
        return true;
    }

    private static bool WriteIPv6Words(Span<ushort> words, int wordCount, int compressIndex, Span<byte> bytes)
    {
        if (compressIndex >= 0)
        {
            int missing = 8 - wordCount;

            if (missing <= 0)
            {
                return false;
            }

            for (int i = wordCount - 1; i >= compressIndex; i--)
            {
                words[i + missing] = words[i];
            }

            for (int i = compressIndex; i < compressIndex + missing; i++)
            {
                words[i] = 0;
            }
        }
        else if (wordCount != 8)
        {
            return false;
        }

        for (int i = 0; i < 8; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes.Slice(i * 2, 2), words[i]);
        }

        return true;
    }
}
