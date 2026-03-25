using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;

namespace Nalix.Debug
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== NALIX EXAMPLE SERVER CLIENT DEBUGGER ===");
            
            string host = "127.0.0.1";
            int port = 57206;

            Console.WriteLine($"Connecting to {host}:{port}...");

            try
            {
                // Create minimal registry
                var registry = new PacketRegistryFactory()
                    .RegisterPacket<Control>()
                    .CreateCatalog();

                var options = new TransportOptions
                {
                    Address = host,
                    Port = (ushort)port,
                    EncryptionEnabled = false
                };

                using var session = new TcpSession(options, registry);
                
                // Set up event handlers
                session.OnMessageReceived += (s, lease) =>
                {
                    Console.WriteLine($"[RCV] Received message. Length: {lease.Length}");
                    // We can try to read the header
                    uint magic = Nalix.Framework.Extensions.HeaderExtensions.ReadMagicNumberLE(lease.Span);
                    ushort opcode = Nalix.Framework.Extensions.HeaderExtensions.ReadOpCodeLE(lease.Span);
                    Console.WriteLine($"[RCV] Magic: 0x{magic:X8}, OpCode: {opcode}");
                };

                session.OnDisconnected += (s, ex) =>
                {
                    Console.WriteLine($"[DIS] Disconnected. Reason: {ex?.Message ?? "Normal"}");
                };

                // Connect
                await session.ConnectAsync(host, (ushort)port, CancellationToken.None);
                Console.WriteLine("[CON] Connected!");

                // Create a Control packet (Magic is auto-generated in PacketBase)
                var ping = new Control();
                ping.OpCode = 100; // Expected by Example server's Ping handler
                
                Console.WriteLine($"[SND] Sending Ping (Control) packet. OpCode: {ping.OpCode}, Magic: 0x{ping.MagicNumber:X8}");

                await session.SendAsync(ping, CancellationToken.None);
                Console.WriteLine("[SND] Sent!");

                // Wait a bit for response
                await Task.Delay(2000);

                await session.DisconnectAsync();
                Console.WriteLine("[CON] Disconnected.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERR] {ex.Message}");
                if (ex.InnerException != null) Console.WriteLine($"[INN] {ex.InnerException.Message}");
            }

            Console.WriteLine("=== DEBUG COMPLETE ===");
        }
    }
}
