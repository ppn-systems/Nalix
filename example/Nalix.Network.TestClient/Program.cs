using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Injection;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

internal static class Program
{
    private const ushort Port = 57206;

    private static async Task Main()
    {
        _ = InstanceManager.Instance.WithLogging(NullLogger.Instance);
        InstanceManager.Instance.Register<ILogger>(NullLogger.Instance);

        PacketRegistryFactory factory = new();
        using TcpSession session = new(
            new TransportOptions
            {
                Address = "127.0.0.1",
                Port = Port,
                ResumeEnabled = true,
                ResumeFallbackToHandshake = true,
                ResumeTimeoutMillis = 2500
            },
            factory.CreateCatalog());

        Console.WriteLine($"client: connecting to 127.0.0.1:{Port}");

        await session.ConnectAsync().ConfigureAwait(false);
        await session.HandshakeAsync().ConfigureAwait(false);
        Console.WriteLine("client: handshake ok");

        double ping = await session.PingAsync(2500).ConfigureAwait(false);
        Console.WriteLine($"client: ping ok => {ping:0.00}ms");

        (double rtt, double adjusted) = await session.SyncTimeAsync(2500).ConfigureAwait(false);
        Console.WriteLine($"client: time sync ok => rtt={rtt:0.00}ms adjusted={adjusted:0.00}");

        await session.UpdateCipherAsync(Nalix.Common.Security.CipherSuiteType.Chacha20Poly1305, 2500).ConfigureAwait(false);
        Console.WriteLine($"client: cipher ok => {session.Options.Algorithm}");

        try
        {
            await session.DisconnectAsync().ConfigureAwait(false);
            bool resumed = await session.ConnectWithResumeAsync().ConfigureAwait(false);
            Console.WriteLine($"client: resume ok => {resumed}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"client: resume failed => {ex.GetType().Name}: {ex.Message}");
            throw;
        }

        bool disconnectedSeen = false;
        using IDisposable sub = session.SubscribeTemp<Control>(
            onMessage: _ => { },
            onDisconnected: _ => disconnectedSeen = true);

        await session.DisconnectGracefullyAsync().ConfigureAwait(false);
        Console.WriteLine($"client: disconnect ok => {disconnectedSeen}");
    }
}
