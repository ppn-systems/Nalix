// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Packets.Abstractions;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Injection.DI;
using Nalix.SDK.Remote.Configuration;

namespace Nalix.SDK.Remote;

/// <summary>
/// Represents a singleton UDP client transport used for sending and receiving packets of type IPacket.
/// This client is designed to communicate with a predefined remote endpoint using the UDP protocol.
/// </summary>
[System.Obsolete("UnreliableClient is deprecated and will be removed in future releases. " +
    "Please use ReliableClient with appropriate configurations for reliable transport.")]
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods |
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)]
[System.Diagnostics.DebuggerDisplay("Remote={Options.Address}:{Options.Port}, IsReceiving={IsReceiving}")]
public sealed class UnreliableClient
    : SingletonBase<UnreliableClient>, System.IDisposable, IAsyncActivatable
{
    #region Fields

    private readonly System.Net.IPEndPoint _remoteEndPoint;
    private readonly System.Net.Sockets.UdpClient _udpClient;

    private readonly IPacketCatalog _catalog = InstanceManager.Instance.GetExistingInstance<IPacketCatalog>()
        ?? throw new System.InvalidOperationException("Packet catalog instance is not registered in the dependency injection container.");

    #endregion Fields

    #region Propierties

    /// <summary>
    /// Gets the configuration context for the remote transport options, including remote IP and port.
    /// </summary>
    public TransportOptions Options { get; }

    /// <summary>
    /// Indicates whether the UDP client is actively running and receiving data.
    /// </summary>
    public System.Boolean IsReceiving { get; private set; }

    #endregion Propierties

    /// <summary>
    /// Initializes a new instance of the <see cref="UnreliableClient"/> class.
    /// Automatically binds to a random local port and resolves the remote endpoint from configuration.
    /// </summary>
    public UnreliableClient()
    {
        Options = ConfigurationManager.Instance.Get<TransportOptions>();

        _udpClient = new System.Net.Sockets.UdpClient(0); // Binds to random local port
        _udpClient.Client.DontFragment = true;
        _udpClient.Client.SendBufferSize = 1 << 16;
        _udpClient.Client.ReceiveBufferSize = 1 << 16;

        _remoteEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(Options.Address), Options.Port);
    }

    /// <summary>
    /// Asynchronous loop that continuously listens for incoming UDP packets.
    /// </summary>
    /// <param name="token">The cancellation token used to stop the loop.</param>
    [System.Runtime.CompilerServices.SkipLocalsInit]
    public async System.Threading.Tasks.Task ActivateAsync(
        System.Threading.CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                System.Net.Sockets.UdpReceiveResult result = await _udpClient.ReceiveAsync(token);
                _ = _catalog.TryDeserialize(System.MemoryExtensions.AsSpan(result.Buffer), out IPacket packet);
            }
            catch (System.OperationCanceledException)
            {
                break;
            }
            catch (System.Exception ex)
            {
                // Optional logging
                System.Diagnostics.Debug.WriteLine($"UDP Receive ERROR: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Stops the UDP client, cancels receiving operations, and disposes internal resources.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    public async System.Threading.Tasks.Task DeactivateAsync(
        System.Threading.CancellationToken token = default)
    {
        _udpClient?.Dispose();

        this.IsReceiving = false;

        await System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a serialized packet asynchronously to the configured remote endpoint.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    [System.Runtime.CompilerServices.SkipLocalsInit]
    public async System.Threading.Tasks.Task SendAsync(IPacket packet)
    {
        System.Memory<System.Byte> memory = packet.Serialize();
        _ = await _udpClient.SendAsync(memory.ToArray(), memory.Length, _remoteEndPoint);
    }

    /// <summary>
    /// Disposes the client and releases all held resources.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await this.DeactivateAsync();
}
