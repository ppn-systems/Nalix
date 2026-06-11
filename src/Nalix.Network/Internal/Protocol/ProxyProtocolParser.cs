// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Net;
using System.Text;
using Nalix.Network.Internal.Transport;

namespace Nalix.Network.Internal.Protocol;

internal static class ProxyProtocolParser
{
    private static ReadOnlySpan<byte> V1Magic => "PROXY "u8;
    private static ReadOnlySpan<byte> V2Magic => "\r\n\r\n\0\r\nQUIT\n"u8;

    // Maximum V1 header is 108 bytes according to HAProxy specification — used to cap stackalloc
    private const int V1MaxLength = 108;

    /// <summary>
    /// Tries to parse a Proxy Protocol V1 or V2 header from <paramref name="buffer"/>.
    /// Zero-allocation: Span slicing + stackalloc (bounded 108 bytes) + BinaryPrimitives only.
    /// </summary>
    /// <returns>
    /// true = header parsed OK. realIp may be null if it is LOCAL/UNKNOWN (pass-through).
    /// false = buffer too short (needs more) or incorrect format (dropped).
    /// </returns>
    public static bool TryParse(ReadOnlySpan<byte> buffer, out IPEndPoint? realIp, out int bytesToConsume)
    {
        realIp = null;
        bytesToConsume = 0;

        if (buffer.Length < 8)
        {
            return false;
        }

        if (buffer.StartsWith(V1Magic))
        {
            return TryParseV1(buffer, out realIp, out bytesToConsume);
        }

        if (buffer.StartsWith(V2Magic))
        {
            return TryParseV2(buffer, out realIp, out bytesToConsume);
        }

        return false;
    }

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

    // ── V1 (text-based) ───────────────────────────────────────────────────────
    private static bool TryParseV1(ReadOnlySpan<byte> buffer, out IPEndPoint? realIp, out int bytesToConsume)
    {
        realIp = null;
        bytesToConsume = 0;

        // SEC: Cap trước stackalloc — attacker không thể trigger stack overflow
        if (buffer.Length > V1MaxLength)
        {
            return false;
        }

        int crlf = buffer.IndexOf("\r\n"u8);
        if (crlf < 0)
        {
            return false;   // Incomplete — caller retries
        }

        bytesToConsume = crlf + 2;

        ReadOnlySpan<byte> line = buffer[..crlf];

        Span<char> chars = stackalloc char[V1MaxLength];
        int charCount = Encoding.UTF8.GetChars(line, chars);
        ReadOnlySpan<char> text = chars[..charCount];

        // Format: "PROXY TCP4/TCP6/UNKNOWN <src-ip> <dst-ip> <src-port> <dst-port>"
        int f1End = text.IndexOf(' ');
        if (f1End < 0)
        {
            return false;
        }

        ReadOnlySpan<char> rest1 = text[(f1End + 1)..];

        int f2End = rest1.IndexOf(' ');
        if (f2End < 0)
        {
            return false;
        }

        ReadOnlySpan<char> proto = rest1[..f2End];
        ReadOnlySpan<char> rest2 = rest1[(f2End + 1)..];

        if (proto.Equals("UNKNOWN", StringComparison.Ordinal))
        {
            return true;   // Pass-through
        }

        int f3End = rest2.IndexOf(' ');
        if (f3End < 0)
        {
            return false;
        }

        ReadOnlySpan<char> srcIp = rest2[..f3End];
        ReadOnlySpan<char> rest3 = rest2[(f3End + 1)..];

        int f4End = rest3.IndexOf(' ');
        if (f4End < 0)
        {
            return false;
        }

        ReadOnlySpan<char> rest4 = rest3[(f4End + 1)..];   // skip dst-ip

        int f5End = rest4.IndexOf(' ');
        if (f5End < 0)
        {
            return false;
        }

        ReadOnlySpan<char> srcPortSpan = rest4[..f5End];

        if (!IPAddress.TryParse(srcIp, out IPAddress? ip))
        {
            return false;
        }

        if (!ushort.TryParse(srcPortSpan, out ushort port))
        {
            return false;
        }

        realIp = new IPEndPoint(ip, port);
        return true;
    }

    // ── V2 (binary-based) ─────────────────────────────────────────────────────
    private static bool TryParseV2(ReadOnlySpan<byte> buffer, out IPEndPoint? realIp, out int bytesToConsume)
    {
        realIp = null;
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
            return false;   // Not V2
        }

        bytesToConsume = 16 + addrLen;
        if (buffer.Length < bytesToConsume)
        {
            return false;   // Incomplete — retry
        }

        byte command = (byte)(versionCmd & 0x0F);
        if (command == 0x00)
        {
            return true;            // LOCAL: health check, pass-through
        }

        if (command != 0x01)
        {
            return false;           // Chỉ hỗ trợ PROXY command
        }

        byte af = (byte)(family >> 4);

        switch (af)
        {
            case 0x01:   // AF_INET (IPv4)
                {
                    if (addrLen < 12)
                    {
                        return false;
                    }

                    IPAddress srcAddr = new(buffer.Slice(16, 4));
                    ushort srcPort = BinaryPrimitives.ReadUInt16BigEndian(buffer[24..26]);
                    realIp = new IPEndPoint(srcAddr, srcPort);
                    return true;
                }
            case 0x02:   // AF_INET6
                {
                    if (addrLen < 36)
                    {
                        return false;
                    }

                    IPAddress srcAddr = new(buffer.Slice(16, 16));
                    ushort srcPort = BinaryPrimitives.ReadUInt16BigEndian(buffer[48..50]);
                    realIp = new IPEndPoint(srcAddr, srcPort);
                    return true;
                }
            case 0x03:   // AF_UNIX — pass-through
                return true;
            default:
                return false;
        }
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
        endpoint = SocketEndpoint.Empty;
        bytesToConsume = 0;

        if (buffer.Length > V1MaxLength)
        {
            return false;
        }

        int crlf = buffer.IndexOf("\r\n"u8);
        if (crlf < 0)
        {
            return false;
        }

        bytesToConsume = crlf + 2;

        ReadOnlySpan<byte> line = buffer[..crlf];

        Span<char> chars = stackalloc char[V1MaxLength];
        int charCount = Encoding.UTF8.GetChars(line, chars);
        ReadOnlySpan<char> text = chars[..charCount];

        int f1End = text.IndexOf(' ');
        if (f1End < 0)
        {
            return false;
        }

        ReadOnlySpan<char> rest1 = text[(f1End + 1)..];
        int f2End = rest1.IndexOf(' ');
        if (f2End < 0)
        {
            return false;
        }

        ReadOnlySpan<char> proto = rest1[..f2End];
        ReadOnlySpan<char> rest2 = rest1[(f2End + 1)..];

        if (proto.Equals("UNKNOWN", StringComparison.Ordinal))
        {
            return true; // pass-through
        }

        int f3End = rest2.IndexOf(' ');
        if (f3End < 0)
        {
            return false;
        }

        ReadOnlySpan<char> srcIp = rest2[..f3End];
        ReadOnlySpan<char> rest3 = rest2[(f3End + 1)..];

        int f4End = rest3.IndexOf(' ');
        if (f4End < 0)
        {
            return false;
        }

        ReadOnlySpan<char> rest4 = rest3[(f4End + 1)..];

        int f5End = rest4.IndexOf(' ');
        if (f5End < 0)
        {
            return false;
        }

        ReadOnlySpan<char> srcPortSpan = rest4[..f5End];

        if (!ushort.TryParse(srcPortSpan, out ushort port))
        {
            return false;
        }

        // Parse IP directly to SocketEndpoint to avoid IPAddress allocation.
        // For IPv4, try to parse "d.d.d.d" format manually.
        Span<byte> addrBytes = stackalloc byte[4];
        if (TryParseIPv4ToBytes(srcIp, addrBytes))
        {
            endpoint = SocketEndpoint.FromRawBytes(addrBytes, port, isIPv6: false);
            return true;
        }

        // IPv6: fall back to IPAddress.TryParse (allocates, but V1+IPv6 is rare)
        if (IPAddress.TryParse(srcIp, out IPAddress? ip))
        {
            endpoint = SocketEndpoint.FromIpAddress(ip);
            return true;
        }

        return false;
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
}
