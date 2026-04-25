#if DEBUG
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Xunit;

namespace Nalix.SDK.Tests;

[Collection("RealServerTests")]
public sealed class TcpSessionIntegrationTests : IDisposable
{
    private readonly TcpListener _listener;
    private readonly int _port;
    private readonly IPacketRegistry _registry;

    public TcpSessionIntegrationTests()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        
        // Use real registry with system packets
        _registry = new PacketRegistryFactory().CreateCatalog();
    }

    [Fact]
    public async Task PingAsync_WithRealTcpConnection_ReturnsRtt()
    {
        using TcpSession session = new(new TransportOptions { EncryptionEnabled = false }, _registry);
        
        // Start "Server" in background
        Task serverTask = RunMockServerAsync();

        await session.ConnectAsync("127.0.0.1", (ushort)_port);
        
        double rtt = await session.PingAsync(timeoutMs: 5000);
        
        Assert.True(rtt >= 0);
        
        await session.DisconnectAsync();
        await serverTask;
    }

    [Fact]
    public async Task UpdateCipherAsync_WithRealTcpConnection_Succeeds()
    {
        using TcpSession session = new(new TransportOptions { EncryptionEnabled = false }, _registry);
        
        // Start "Server" in background
        Task serverTask = RunMockServerAsync();

        await session.ConnectAsync("127.0.0.1", (ushort)_port);
        
        // Update to Chacha20Poly1305
        await session.UpdateCipherAsync(CipherSuiteType.Chacha20Poly1305, timeoutMs: 5000);
        
        Assert.Equal(CipherSuiteType.Chacha20Poly1305, session.Options.Algorithm);
        
        await session.DisconnectAsync();
        await serverTask;
    }

    private async Task RunMockServerAsync()
    {
        using Socket serverSocket = await _listener.AcceptSocketAsync();
        byte[] lengthBuffer = new byte[2];
        
        // Loop to handle multiple requests if needed, or just handle one and exit
        while (true)
        {
            int received = 0;
            try
            {
                received = await serverSocket.ReceiveAsync(lengthBuffer, SocketFlags.None);
            }
            catch (SocketException) { break; }
            
            if (received != 2) break;
            
            ushort totalLength = BitConverter.ToUInt16(lengthBuffer, 0);
            int payloadLength = totalLength - 2;
            byte[] payload = new byte[payloadLength];
            await serverSocket.ReceiveAsync(payload, SocketFlags.None);
            
            IPacket pkt = _registry.Deserialize(payload);
            
            if (pkt is Control ctrl && ctrl.Type == ControlType.PING)
            {
                // Send PONG
                Control pong = Control.Create();
                pong.Initialize(0, ControlType.PONG, ctrl.SequenceId, PacketFlags.RELIABLE, ProtocolReason.NONE);
                
                byte[] pongData = new byte[pong.Length];
                pong.Serialize(pongData);
                
                byte[] frame = new byte[2 + pongData.Length];
                BitConverter.TryWriteBytes(frame.AsSpan(0, 2), (ushort)(2 + pongData.Length));
                pongData.CopyTo(frame.AsSpan(2));
                
                await serverSocket.SendAsync(frame, SocketFlags.None);
            }
            else if (pkt is Control update && update.Type == ControlType.CIPHER_UPDATE)
            {
                // Send CIPHER_UPDATE_ACK
                Control ack = Control.Create();
                ack.Initialize(0, ControlType.CIPHER_UPDATE_ACK, update.SequenceId, PacketFlags.RELIABLE, ProtocolReason.NONE);
                
                byte[] ackData = new byte[ack.Length];
                ack.Serialize(ackData);
                
                byte[] frame = new byte[2 + ackData.Length];
                BitConverter.TryWriteBytes(frame.AsSpan(0, 2), (ushort)(2 + ackData.Length));
                ackData.CopyTo(frame.AsSpan(2));
                
                await serverSocket.SendAsync(frame, SocketFlags.None);
            }
        }
        
        // Give client time to receive final frame
        await Task.Delay(100);
    }

    public void Dispose()
    {
        _listener.Stop();
    }
}
#endif
