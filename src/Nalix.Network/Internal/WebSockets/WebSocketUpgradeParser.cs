// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

#pragma warning disable CA5350 // SHA1 is required by RFC 6455

namespace Nalix.Network.Internal.WebSockets;

/// <summary>
/// Zero-allocation parser for HTTP/1.1 WebSocket upgrade requests.
/// </summary>
internal static class WebSocketUpgradeParser
{
    private static ReadOnlySpan<byte> DoubleCrlf => "\r\n\r\n"u8;
    private static ReadOnlySpan<byte> Crlf => "\r\n"u8;

    // Header keys
    private static ReadOnlySpan<byte> UpgradeKey => "upgrade:"u8;
    private static ReadOnlySpan<byte> ConnectionKey => "connection:"u8;
    private static ReadOnlySpan<byte> SecWebSocketVersionKey => "sec-websocket-version:"u8;
    private static ReadOnlySpan<byte> SecWebSocketKeyKey => "sec-websocket-key:"u8;
    private static ReadOnlySpan<byte> OriginKey => "origin:"u8;
    private static ReadOnlySpan<byte> SecWebSocketProtocolKey => "sec-websocket-protocol:"u8;

    // Header expected values
    private static ReadOnlySpan<byte> UpgradeWebSocketValue => "websocket"u8;
    private static ReadOnlySpan<byte> ConnectionUpgradeValue => "upgrade"u8;
    private static ReadOnlySpan<byte> Version13Value => "13"u8;

    // RFC 6455 magic string
    private static ReadOnlySpan<byte> MagicString => "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"u8;

    /// <summary>
    /// Parses an HTTP/1.1 request to extract WebSocket upgrade headers.
    /// </summary>
    /// <param name="buffer">The received bytes containing the HTTP request.</param>
    /// <returns>A parsed result indicating success or failure and the extracted values.</returns>
    public static WebSocketUpgradeResult Parse(ReadOnlySpan<byte> buffer)
    {
        int headerEnd = buffer.IndexOf(DoubleCrlf);
        if (headerEnd < 0)
        {
            return new WebSocketUpgradeResult { IsValid = false }; // Not complete yet
        }

        int totalBytesConsumed = headerEnd + 4; // Including \r\n\r\n
        ReadOnlySpan<byte> headersSpan = buffer[..headerEnd];

        // 1. Parse Request Line
        int firstLineEnd = headersSpan.IndexOf(Crlf);
        if (firstLineEnd <= 0)
        {
            return new WebSocketUpgradeResult { IsValid = false, BytesConsumed = totalBytesConsumed };
        }

        ReadOnlySpan<byte> requestLine = headersSpan[..firstLineEnd];
        headersSpan = headersSpan[(firstLineEnd + 2)..];

        int methodEnd = requestLine.IndexOf((byte)' ');
        if (methodEnd <= 0)
        {
            return new WebSocketUpgradeResult { IsValid = false, BytesConsumed = totalBytesConsumed };
        }

        ReadOnlySpan<byte> method = requestLine[..methodEnd];
        ReadOnlySpan<byte> remainingRequestLine = requestLine[(methodEnd + 1)..];

        int pathEnd = remainingRequestLine.IndexOf((byte)' ');
        if (pathEnd <= 0)
        {
            return new WebSocketUpgradeResult { IsValid = false, BytesConsumed = totalBytesConsumed };
        }

        ReadOnlySpan<byte> path = remainingRequestLine[..pathEnd];

        // 2. Parse Headers
        bool hasUpgrade = false;
        bool hasConnection = false;
        bool hasVersion = false;
        ReadOnlySpan<byte> secWebSocketKey = default;
        ReadOnlySpan<byte> origin = default;
        ReadOnlySpan<byte> subProtocol = default;

        Span<byte> lowerKeyBuffer = stackalloc byte[128]; // Max expected header key length

        while (headersSpan.Length > 0)
        {
            int lineEnd = headersSpan.IndexOf(Crlf);
            ReadOnlySpan<byte> line = lineEnd >= 0 ? headersSpan[..lineEnd] : headersSpan;

            int colonIndex = line.IndexOf((byte)':');
            if (colonIndex > 0)
            {
                ReadOnlySpan<byte> key = TrimSpace(line[..colonIndex]);
                ReadOnlySpan<byte> value = TrimSpace(line[(colonIndex + 1)..]);

                if (key.Length <= lowerKeyBuffer.Length)
                {
                    Span<byte> lowerKey = lowerKeyBuffer[..key.Length];
                    ToLowerAscii(key, lowerKey);

                    if (lowerKey.SequenceEqual(UpgradeKey[..^1])) // Exclude colon
                    {
                        if (ContainsIgnoreCase(value, UpgradeWebSocketValue))
                        {
                            hasUpgrade = true;
                        }
                    }
                    else if (lowerKey.SequenceEqual(ConnectionKey[..^1]))
                    {
                        if (ContainsIgnoreCase(value, ConnectionUpgradeValue))
                        {
                            hasConnection = true;
                        }
                    }
                    else if (lowerKey.SequenceEqual(SecWebSocketVersionKey[..^1]))
                    {
                        if (value.SequenceEqual(Version13Value))
                        {
                            hasVersion = true;
                        }
                    }
                    else if (lowerKey.SequenceEqual(SecWebSocketKeyKey[..^1]))
                    {
                        secWebSocketKey = value;
                    }
                    else if (lowerKey.SequenceEqual(OriginKey[..^1]))
                    {
                        origin = value;
                    }
                    else if (lowerKey.SequenceEqual(SecWebSocketProtocolKey[..^1]))
                    {
                        subProtocol = value;
                    }
                }
            }

            if (lineEnd < 0)
            {
                break;
            }
            headersSpan = headersSpan[(lineEnd + 2)..];
        }

        // Validate all required fields are present
        bool isValid = hasUpgrade && hasConnection && hasVersion && !secWebSocketKey.IsEmpty && secWebSocketKey.Length == 24;

        return new WebSocketUpgradeResult
        {
            IsValid = isValid,
            SecWebSocketKey = secWebSocketKey,
            Path = path,
            Origin = origin,
            SubProtocol = subProtocol,
            HttpMethod = method,
            BytesConsumed = totalBytesConsumed
        };
    }

    /// <summary>
    /// Computes the Sec-WebSocket-Accept key required for the handshake response.
    /// </summary>
    /// <param name="clientKey">The Sec-WebSocket-Key from the client request (must be 24 bytes).</param>
    /// <param name="destination">The destination span for the base64 encoded result.</param>
    /// <returns>The number of bytes written to the destination, or 0 if failed.</returns>
    public static int ComputeAcceptKey(ReadOnlySpan<byte> clientKey, Span<byte> destination)
    {
        if (clientKey.Length != 24)
        {
            return 0;
        }

        Span<byte> combined = stackalloc byte[60]; // 24 bytes key + 36 bytes magic string
        clientKey.CopyTo(combined);
        MagicString.CopyTo(combined[24..]);

        Span<byte> hash = stackalloc byte[20]; // SHA-1 is 20 bytes
        _ = SHA1.HashData(combined, hash);

        _ = System.Buffers.Text.Base64.EncodeToUtf8(hash, destination, out _, out int bytesWritten);
        return bytesWritten;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ToLowerAscii(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        for (int i = 0; i < source.Length; i++)
        {
            byte c = source[i];
            destination[i] = (c >= 'A' && c <= 'Z') ? (byte)(c + 32) : c;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ContainsIgnoreCase(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target)
    {
        if (source.Length < target.Length)
        {
            return false;
        }

        Span<byte> lowerSource = stackalloc byte[source.Length];
        ToLowerAscii(source, lowerSource);
        return lowerSource.IndexOf(target) >= 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<byte> TrimSpace(ReadOnlySpan<byte> source)
    {
        int start = 0;
        while (start < source.Length && source[start] <= 32)
        {
            start++;
        }

        int end = source.Length - 1;
        while (end >= start && source[end] <= 32)
        {
            end--;
        }

        return source.Slice(start, end - start + 1);
    }
}
