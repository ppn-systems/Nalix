using Nalix.Common.Package;
using Nalix.Shared.Injection.DI;

namespace Nalix.Network.Client;

/// <summary>
/// Represents a network client that connects to a remote server using TCP.
/// </summary>
/// <remarks>
/// The <see cref="NetworkClient{TPacket}"/> class is a singleton that manages the connection,
/// network stream, and client disposal. It supports both synchronous and asynchronous connection.
/// </remarks>
public class NetworkClient<TPacket> : SingletonBase<NetworkClient<TPacket>>, System.IDisposable
    where TPacket : IPacket, IPacketFactory<TPacket>, IPacketDeserializer<TPacket>
{
    private NetworkSender<TPacket> _sender;
    private NetworkReceiver<TPacket> _receiver;
    private System.Net.Sockets.TcpClient _client;
    private System.Net.Sockets.NetworkStream _stream;

    /// <summary>
    /// Gets the context associated with the network connection.
    /// </summary>
    public NetworkContext Context { get; } = new();

    /// <summary>
    /// Gets the network sender used to send packets.
    /// </summary>
    public NetworkSender<TPacket> Sender => _sender;

    /// <summary>
    /// Gets the network receiver used to receive packets.
    /// </summary>
    public NetworkReceiver<TPacket> Receiver => _receiver;

    /// <summary>
    /// Gets the <see cref="System.Net.Sockets.NetworkStream"/> used for network communication.
    /// </summary>
    public System.Net.Sockets.NetworkStream Stream => _stream;

    /// <summary>
    /// Gets a value indicating whether the client is connected to the server.
    /// </summary>
    public bool IsConnected => _client?.Connected == true && _stream != null;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkClient{TPacket}"/> class.
    /// </summary>
    private NetworkClient() => _client = new System.Net.Sockets.TcpClient { NoDelay = true };

    /// <summary>
    /// Connects to a remote server synchronously within a specified timeout period.
    /// </summary>
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
            _sender = new NetworkSender<TPacket>(_stream);
            _receiver = new NetworkReceiver<TPacket>(_stream);
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
            _sender = new NetworkSender<TPacket>(_stream);
            _receiver = new NetworkReceiver<TPacket>(_stream);
        }
        catch (System.Exception ex)
        {
            // Handle specific exceptions like SocketException if needed
            throw new System.InvalidOperationException("Failed to connect", ex);
        }
    }

    /// <summary>
    /// Releases the resources used by the <see cref="NetworkClient{TPacket}"/> instance.
    /// </summary>
    public new void Dispose()
    {
        _stream?.Dispose();
        _client?.Close();

        _sender = null;
        _receiver = null;

        System.GC.SuppressFinalize(this);
    }
}
