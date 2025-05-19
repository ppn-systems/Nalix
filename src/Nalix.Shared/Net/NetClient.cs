using Nalix.Common.Package;
using Nalix.Shared.Injection.DI;
using Nalix.Shared.Net.Transport;

namespace Nalix.Shared.Net;

/// <summary>
/// Represents a network client that connects to a remote server using Reliable.
/// </summary>
/// <remarks>
/// The <see cref="NetClient{TPacket}"/> class is a singleton that manages the connection,
/// network stream, and client disposal. It supports both synchronous and asynchronous connection.
/// </remarks>
public class NetClient<TPacket> : SingletonBase<NetClient<TPacket>>, System.IDisposable
    where TPacket : IPacket, IPacketFactory<TPacket>, IPacketDeserializer<TPacket>
{
    private NetSender<TPacket>? _sender;
    private NetReader<TPacket>? _reader;
    private System.Net.Sockets.TcpClient _client;
    private System.Net.Sockets.NetworkStream? _stream;

    /// <summary>
    /// Gets the context associated with the network connection.
    /// </summary>
    public NetContext Context { get; } = new();

    /// <summary>
    /// Gets the network sender used to send packets.
    /// </summary>
    public NetSender<TPacket> Sender => _sender
        ?? throw new System.InvalidOperationException("Sender is not initialized.");

    /// <summary>
    /// Gets the network receiver used to receive packets.
    /// </summary>
    public NetReader<TPacket> Receiver => _reader
        ?? throw new System.InvalidOperationException("Receiver is not initialized.");

    /// <summary>
    /// Gets the <see cref="System.Net.Sockets.NetworkStream"/> used for network communication.
    /// </summary>
    public System.Net.Sockets.NetworkStream Stream => _stream
        ?? throw new System.InvalidOperationException("Stream is not initialized.");

    /// <summary>
    /// Gets a value indicating whether the client is connected to the server.
    /// </summary>
    public bool IsConnected => _client?.Connected == true && _stream != null;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetClient{TPacket}"/> class.
    /// </summary>
    private NetClient() => _client = new System.Net.Sockets.TcpClient { NoDelay = true };

    /// <summary>
    /// Connects to a remote server synchronously within a specified timeout period.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Connect(
        int timeout = 30000,
        System.Threading.CancellationToken cancellationToken = default)
    {
        _client?.Close();
        _client = new System.Net.Sockets.TcpClient { NoDelay = true };

        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            _client.Connect(Context.Address, Context.Port); // Synchronous Connect

            _stream = _client.GetStream();
            _sender = new NetSender<TPacket>(_stream);
            _reader = new NetReader<TPacket>(_stream);
        }
        catch (System.Exception ex)
        {
            // Handle specific exceptions like SocketException if needed
            throw new System.InvalidOperationException("Failed to connect", ex);
        }
    }

    /// <summary>
    /// Asynchronously connects to a remote server within a specified timeout period.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async System.Threading.Tasks.Task ConnectAsync(
        int timeout = 30000,
        System.Threading.CancellationToken cancellationToken = default)
    {
        _client?.Close();
        _client = new System.Net.Sockets.TcpClient { NoDelay = true };

        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await _client.ConnectAsync(Context.Address, Context.Port, cts.Token);

            _stream = _client.GetStream();
            _sender = new NetSender<TPacket>(_stream);
            _reader = new NetReader<TPacket>(_stream);
        }
        catch (System.Exception ex)
        {
            // Handle specific exceptions like SocketException if needed
            throw new System.InvalidOperationException("Failed to connect", ex);
        }
    }

    /// <summary>
    /// Closes the network connection and releases resources.
    /// </summary>
    public void Close() => this.Dispose();

    /// <summary>
    /// Releases the resources used by the <see cref="NetClient{TPacket}"/> instance.
    /// </summary>
    public new void Dispose()
    {
        _stream?.Dispose();
        _client?.Close();

        _sender = null;
        _reader = null;

        System.GC.SuppressFinalize(this);
    }
}
