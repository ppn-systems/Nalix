// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Abstractions;
using Nalix.SDK.Remote.Internal;
using Nalix.Shared.Configuration;

namespace Nalix.SDK.Remote.Core;

/// <summary>
/// Represents a network client that connects to a remote server using Reliable.
/// </summary>
/// <remarks>
/// The <see cref="RemoteStreamClient{TPacket}"/> class is a singleton that manages the connection,
/// network stream, and client disposal. It supports both synchronous and asynchronous connection.
/// </remarks>
[System.Diagnostics.DebuggerDisplay("Remote={Options.Address}:{Options.Port}, Connected={IsConnected}")]
public class RemoteStreamClient<TPacket> : System.IDisposable where TPacket : IPacket
{
    #region Fields

    private System.Net.Sockets.TcpClient _client;
    private System.Net.Sockets.NetworkStream _stream;

    private RemoteStreamSender<TPacket> _outbound;
    private RemoteStreamReceiver<TPacket> _inbound;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the context associated with the network connection.
    /// </summary>
    public RemoteTransportOptions Options { get; }

    /// <summary>
    /// Gets a value indicating whether the client is connected to the server.
    /// </summary>
    public System.Boolean IsConnected => _client?.Connected == true && _stream != null;

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteStreamClient{TPacket}"/> class.
    /// </summary>
    private RemoteStreamClient()
    {
        this.Options = ConfigurationManager.Instance.Get<RemoteTransportOptions>();

        _client = new System.Net.Sockets.TcpClient { NoDelay = true };
    }

    #endregion Constructor

    #region APIs

    /// <summary>
    /// Asynchronously connects to a remote server within a specified timeout period.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.SkipLocalsInit]
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_stream))]
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_outbound))]
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_inbound))]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.Task ConnectAsync(
        System.Int32 timeout = 30000,
        System.Threading.CancellationToken cancellationToken = default)
    {
        _client?.Close();
        _client = new System.Net.Sockets.TcpClient { NoDelay = true };

        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await _client.ConnectAsync(Options.Address, Options.Port, cts.Token);

            _stream = _client.GetStream();
            _outbound = new RemoteStreamSender<TPacket>(_stream);
            _inbound = new RemoteStreamReceiver<TPacket>(_stream);
        }
        catch (System.Exception ex)
        {
            // Token specific exceptions like SocketException if needed
            throw new System.InvalidOperationException("Failed to connect", ex);
        }
    }

    /// <summary>
    /// Asynchronously sends a packet over the active connection.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    /// <param name="ct">A token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if the client is not connected.
    /// </exception>
    /// <exception cref="System.OperationCanceledException">
    /// Thrown if <paramref name="ct"/> is canceled before or during the send.
    /// </exception>
    /// <exception cref="System.IO.IOException">
    /// Thrown if an I/O error occurs while writing to the underlying stream.
    /// </exception>
    public System.Threading.Tasks.Task SendAsync(
        TPacket packet,
        System.Threading.CancellationToken ct = default)
        => (_outbound ?? throw new System.InvalidOperationException("Not connected.")).SendAsync(packet, ct);

    /// <summary>
    /// Asynchronously receives the next packet from the active connection.
    /// </summary>
    /// <param name="ct">A token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous receive operation and yields the packet.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if the client is not connected or the stream is not readable.
    /// </exception>
    /// <exception cref="System.OperationCanceledException">
    /// Thrown if <paramref name="ct"/> is canceled before or during the receive.
    /// </exception>
    /// <exception cref="System.IO.EndOfStreamException">
    /// Thrown if the stream ends unexpectedly while reading a packet.
    /// </exception>
    /// <exception cref="System.IO.IOException">
    /// Thrown if an I/O error occurs while reading from the underlying stream.
    /// </exception>
    public System.Threading.Tasks.Task<TPacket> ReceiveAsync(System.Threading.CancellationToken ct = default)
        => (_inbound ?? throw new System.InvalidOperationException("Not connected.")).ReceiveAsync(ct);

    /// <summary>
    /// Closes the network connection and releases resources.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    public void Disconnect() => this.Dispose();

    /// <summary>
    /// Releases the resources used by the <see cref="RemoteStreamClient{TPacket}"/> instance.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    public void Dispose()
    {
        _stream?.Dispose();
        _client?.Close();

        _outbound = null;
        _inbound = null;

        System.GC.SuppressFinalize(this);
    }

    #endregion APIs
}