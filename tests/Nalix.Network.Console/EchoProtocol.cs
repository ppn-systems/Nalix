using Nalix.Common.Networking;
using Nalix.Framework.Injection;
using Nalix.Network.Connections;
using Nalix.Network.Protocols;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

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
        InstanceManager.Instance.GetOrCreateInstance<ConnectionHub>()
                                .RegisterConnection(connection);
    }


    public override void ProcessMessage(Object sender, IConnectEventArgs args)
    {
        try
        {
            Console.WriteLine($"[Server] Received message from client {args.Connection.RemoteEndPoint}");

            // Defensive copy: avoid pooled-buffer lifetime issues.
            ReadOnlySpan<Byte> incomingSpan = args.Lease.Span;
            Byte[] payload = incomingSpan.ToArray();

            // Log raw bytes (hex) truncated for safety.
            String incomingHex = ToHexString(payload, 128);
            Console.WriteLine($"[Server][DEBUG] Incoming bytes len={payload.Length} hex={incomingHex}");

            // Attempt strict UTF-8 decode first (throw on invalid bytes).
            String message;
            var utf8Throw = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
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
            String response = $"Server Received: {message}";
            Byte[] responseData = Encoding.UTF8.GetBytes(response);

            // Log outgoing payload details (truncated hex and length).
            String responseHex = ToHexString(responseData, 64);
            Console.WriteLine($"[Server][DEBUG] Sending {responseData.Length} bytes to {args.Connection.RemoteEndPoint} hex={responseHex}");

            // Send and verify result.
            Boolean sent = args.Connection.TCP.Send(responseData);
            if (!sent)
            {
                Console.WriteLine($"[Server][ERROR] Send returned false for {args.Connection.RemoteEndPoint}. Connection may be closed or reset. outgoingHex={responseHex}");
            }
        }
        catch (ObjectDisposedException ode)
        {
            Console.WriteLine($"[Server][ERROR] ObjectDisposedException in {nameof(ProcessMessage)}: {ode.ObjectName} - {ode.Message}");
            Console.WriteLine(ode.ToString());
        }
        catch (SocketException se)
        {
            Console.WriteLine($"[Server][SOCKET] SocketException when sending to {args.Connection.RemoteEndPoint}: SocketErrorCode={se.SocketErrorCode} ErrorCode={se.ErrorCode} Message={se.Message}");
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
    private static String ToHexString(Byte[] data, Int32 maxBytes)
    {
        if (data == null || data.Length == 0)
        {
            return "<empty>";
        }

        Int32 show = Math.Min(data.Length, maxBytes);
        String hex = Convert.ToHexString(data, 0, show);
        if (data.Length > show)
        {
            hex += "...";
        }

        return $"len={data.Length} hex={hex}";
    }
}
