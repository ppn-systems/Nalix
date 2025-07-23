using Nalix.Common.Packets;
using Nalix.SDK.Remote.Internal;
using Nalix.Shared.Configuration;
using Nalix.Shared.Injection.DI;

namespace Nalix.SDK.Remote;

/// <summary>
/// Represents a network client that connects to a remote server using Reliable.
/// </summary>
/// <remarks>
/// The <see cref="RemoteStreamClient{TPacket}"/> class is a singleton that manages the connection,
/// network stream, and client disposal. It supports both synchronous and asynchronous connection.
/// </remarks>
public class RemoteStreamClient<TPacket> : SingletonBase<RemoteStreamClient<TPacket>>, System.IDisposable
    where TPacket : IPacket, IPacketFactory<TPacket>, IPacketDeserializer<TPacket>
{
    #region Fields

    private System.Net.Sockets.TcpClient _client;
    private RemoteStreamSender<TPacket> _sender;
    private RemoteStreamReceiver<TPacket> _reader;
    private System.Net.Sockets.NetworkStream _stream;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the context associated with the network connection.
    /// </summary>
    public RemoteTransportOptions Context { get; }

    ///// <summary>
    ///// Gets the network sender used to send packets.
    ///// </summary>
    //public RemoteStreamSender<TPacket> Sender => _sender
    //    ?? throw new System.InvalidOperationException("Sender is not initialized.");

    ///// <summary>
    ///// Gets the network receiver used to receive packets.
    ///// </summary>
    //public RemoteStreamReceiver<TPacket> Receiver => _reader
    //    ?? throw new System.InvalidOperationException("Receiver is not initialized.");

    /// <summary>
    /// Gets the <see cref="System.Net.Sockets.NetworkStream"/> used for network communication.
    /// </summary>
    public System.Net.Sockets.NetworkStream Stream => _stream
        ?? throw new System.InvalidOperationException("Stream is not initialized.");

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
        this.Context = ConfigurationStore.Instance.Get<RemoteTransportOptions>();

        _client = new System.Net.Sockets.TcpClient { NoDelay = true };
    }

    #endregion Constructor

    #region APIs

    /// <summary>
    /// Connects to a remote server synchronously within a specified timeout period.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Connect(
        System.Int32 timeout = 20000,
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
            _sender = new RemoteStreamSender<TPacket>(_stream);
            _reader = new RemoteStreamReceiver<TPacket>(_stream);
        }
        catch (System.Exception ex)
        {
            // Token specific exceptions like SocketException if needed
            throw new System.InvalidOperationException("Failed to connect", ex);
        }
    }

    /// <summary>
    /// Asynchronously connects to a remote server within a specified timeout period.
    /// </summary>
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
            await _client.ConnectAsync(Context.Address, Context.Port, cts.Token);

            _stream = _client.GetStream();
            _sender = new RemoteStreamSender<TPacket>(_stream);
            _reader = new RemoteStreamReceiver<TPacket>(_stream);
        }
        catch (System.Exception ex)
        {
            // Token specific exceptions like SocketException if needed
            throw new System.InvalidOperationException("Failed to connect", ex);
        }
    }

    /// <summary>
    /// Closes the network connection and releases resources.
    /// </summary>
    public void Close() => this.Dispose();

    /// <summary>
    /// Releases the resources used by the <see cref="RemoteStreamClient{TPacket}"/> instance.
    /// </summary>
    public new void Dispose()
    {
        _stream?.Dispose();
        _client?.Close();

        _sender = null;
        _reader = null;

        System.GC.SuppressFinalize(this);
    }

    #endregion APIs
}