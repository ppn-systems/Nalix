// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Nalix.Common.Networking;
using Nalix.Framework.Injection;
using Nalix.Network.Connections;
using Nalix.Network.Protocols;

public class EchoProtocol : Protocol
{
    public EchoProtocol()
    {
        KeepConnectionOpen = true;
        IsAccepting = true;
    }

    public override void OnAccept(IConnection connection, CancellationToken cancellationToken = default)
    {
        base.OnAccept(connection, cancellationToken);
        _ = InstanceManager.Instance.GetOrCreateInstance<ConnectionHub>()
                                .RegisterConnection(connection);
    }


    public override void ProcessMessage(object sender, IConnectEventArgs args)
    {
        try
        {
            Console.WriteLine($"[Server] Received message from client {args.Connection.NetworkEndpoint}");

            // Defensive copy: avoid pooled-buffer lifetime issues.
            ReadOnlySpan<byte> incomingSpan = args.Lease.Span;
            byte[] payload = incomingSpan.ToArray();

            // Log raw bytes (hex) truncated for safety.
            string incomingHex = ToHexString(payload, 128);
            Console.WriteLine($"[Server][DEBUG] Incoming bytes len={payload.Length} hex={incomingHex}");

            // Attempt strict UTF-8 decode first (throw on invalid bytes).
            string message;
            UTF8Encoding utf8Throw = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            try
            {
                message = utf8Throw.GetString(payload);
                Console.WriteLine($"[Server] Decoded as UTF-8: {message}");
            }
            catch (DecoderFallbackException)
            {
                // UTF-8 failed -> try UTF-16LE (common for Windows clients) as a fallback.
                try
                {
                    message = Encoding.Unicode.GetString(payload);
                    Console.WriteLine($"[Server] Decoded as UTF-16LE: {message}");
                }
                catch
                {
                    // Not UTF-8 or UTF-16LE: treat as binary.
                    message = "<binary-or-unknown-encoding>";
                    Console.WriteLine($"[Server] Warning: payload is not valid UTF-8 nor UTF-16LE. Treating as binary.");
                }
            }

            // Build response based on decoded message (or binary marker).
            string response = $"Server Received: {message}";
            byte[] responseData = Encoding.UTF8.GetBytes(response);

            // Log outgoing payload details (truncated hex and length).
            string responseHex = ToHexString(responseData, 64);
            Console.WriteLine($"[Server][DEBUG] Sending {responseData.Length} bytes to {args.Connection.NetworkEndpoint} hex={responseHex}");

            // Send and verify result.
            bool sent = args.Connection.TCP.Send(responseData);
            if (!sent)
            {
                Console.WriteLine($"[Server][ERROR] Send returned false for {args.Connection.NetworkEndpoint}. Connection may be closed or reset. outgoingHex={responseHex}");
            }
        }
        catch (ObjectDisposedException ode)
        {
            Console.WriteLine($"[Server][ERROR] ObjectDisposedException in {nameof(ProcessMessage)}: {ode.ObjectName} - {ode.Message}");
            Console.WriteLine(ode.ToString());
        }
        catch (SocketException se)
        {
            Console.WriteLine($"[Server][SOCKET] SocketException when sending to {args.Connection.NetworkEndpoint}: SocketErrorCode={se.SocketErrorCode} ErrorCode={se.ErrorCode} Message={se.Message}");
            Console.WriteLine(se.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Server][ERROR] Exception in {nameof(ProcessMessage)}: {ex}");
        }
        finally
        {
            args.Dispose();
        }
    }

    /// <summary>
    /// Convert up to maxBytes of data to an uppercase hex string. Append "..." if truncated.
    /// </summary>
    private static string ToHexString(byte[] data, int maxBytes)
    {
        if (data == null || data.Length == 0)
        {
            return "<empty>";
        }

        int show = Math.Min(data.Length, maxBytes);
        string hex = Convert.ToHexString(data, 0, show);
        if (data.Length > show)
        {
            hex += "...";
        }

        return $"len={data.Length} hex={hex}";
    }
}
