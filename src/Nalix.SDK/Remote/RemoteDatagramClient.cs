﻿using Nalix.Common.Packets;
using Nalix.Shared.Configuration;
using Nalix.Shared.Injection.DI;

namespace Nalix.SDK.Remote;

/// <summary>
/// Represents a singleton UDP client transport used for sending and receiving packets of type <typeparamref name="TPacket"/>.
/// This client is designed to communicate with a predefined remote endpoint using the UDP protocol.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that must implement <see cref="IPacket"/>, <see cref="IPacketTransformer{TPacket}"/>, and <see cref="IPacketTransformer{TPacket}"/>.
/// </typeparam>
public class RemoteDatagramClient<TPacket> : SingletonBase<RemoteDatagramClient<TPacket>>, System.IDisposable
    where TPacket : IPacket, IPacketTransformer<TPacket>
{
    #region Fields

    private readonly System.Net.Sockets.UdpClient _udpClient;
    private readonly System.Net.IPEndPoint _remoteEndPoint;

    private System.Threading.CancellationTokenSource _cts;

    #endregion Fields

    #region Propierties

    /// <summary>
    /// Gets the configuration context for the remote transport options, including remote IP and port.
    /// </summary>
    public RemoteTransportOptions Context { get; }

    /// <summary>
    /// Indicates whether the UDP client is actively running and receiving data.
    /// </summary>
    public System.Boolean IsRunning { get; private set; }

    /// <summary>
    /// Occurs when a valid packet is received from a remote endpoint.
    /// </summary>
    public event System.Action<TPacket, System.Net.IPEndPoint> OnPacketReceived;

    #endregion Propierties

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteDatagramClient{TPacket}"/> class.
    /// Automatically binds to a random local port and resolves the remote endpoint from configuration.
    /// </summary>
    private RemoteDatagramClient()
    {
        Context = ConfigurationStore.Instance.Get<RemoteTransportOptions>();

        _udpClient = new System.Net.Sockets.UdpClient(0); // Binds to random local port
        _udpClient.Client.DontFragment = true;
        _udpClient.Client.ReceiveBufferSize = 1 << 16;
        _udpClient.Client.SendBufferSize = 1 << 16;

        _remoteEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(Context.Address), Context.Port);
    }

    /// <summary>
    /// Starts the receiving loop asynchronously.
    /// </summary>
    /// <param name="externalToken">An optional external cancellation token to allow controlled shutdown.</param>
    public void Start(System.Threading.CancellationToken externalToken = default)
    {
        if (this.IsRunning)
        {
            return;
        }

        _cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        _ = ReceiveLoopAsync(_cts.Token);

        this.IsRunning = true;
    }

    /// <summary>
    /// Stops the UDP client, cancels receiving operations, and disposes internal resources.
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        _udpClient?.Dispose();

        this.IsRunning = false;
    }

    /// <summary>
    /// Sends a serialized packet asynchronously to the configured remote endpoint.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    public async System.Threading.Tasks.Task SendAsync(TPacket packet)
    {
        System.Memory<System.Byte> memory = packet.Serialize();
        _ = await _udpClient.SendAsync(memory.ToArray(), memory.Length, _remoteEndPoint);
    }

    /// <summary>
    /// Asynchronous loop that continuously listens for incoming UDP packets and raises the <see cref="OnPacketReceived"/> event.
    /// </summary>
    /// <param name="token">The cancellation token used to stop the loop.</param>
    private async System.Threading.Tasks.Task ReceiveLoopAsync(
        System.Threading.CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                System.Net.Sockets.UdpReceiveResult result = await _udpClient.ReceiveAsync(token);
                TPacket packet = TPacket.Deserialize(result.Buffer);
                this.OnPacketReceived?.Invoke(packet, result.RemoteEndPoint);
            }
            catch (System.OperationCanceledException)
            {
                break;
            }
            catch (System.Exception ex)
            {
                // Optional logging
                System.Diagnostics.Debug.WriteLine($"UDP Receive Error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Disposes the client and releases all held resources.
    /// </summary>
    public new void Dispose()
    {
        this.Stop();
        System.GC.SuppressFinalize(this);
    }
}
