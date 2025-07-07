using Nalix.Common.Logging;
using Nalix.Network.Configurations;
using Nalix.Network.Protocols;

namespace Nalix.Network.Listeners.Internal;

internal class UdpIngressWorker
{
    #region Fields

    private readonly ILogger _logger;
    private readonly IProtocol _protocol;
    private readonly System.Net.Sockets.Socket _listener;

    private volatile bool _isEnabled;
    private volatile bool _isRunning;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current state of the UDP listener.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Enables or disables the UDP receive loop.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isRunning)
                throw new System.InvalidOperationException("Cannot change IsUdpEnabled while listening.");
            _isEnabled = value;
        }
    }

    #endregion Properties

    #region Constructor

    public UdpIngressWorker(ILogger logger, IProtocol protocol)
    {
        _logger = logger;
        _protocol = protocol;

        _listener = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Dgram,
            System.Net.Sockets.ProtocolType.Udp)
        {
            ExclusiveAddressUse = !Listener.Config.ReuseAddress
        };

        _listener.SetSocketOption(
            System.Net.Sockets.SocketOptionLevel.Socket,
            System.Net.Sockets.SocketOptionName.ReuseAddress,
            Listener.Config.ReuseAddress ? SocketSettings.True : SocketSettings.False);

        _listener.Bind(
            new System.Net.IPEndPoint(System.Net.IPAddress.Any, Listener.Config.Port));
        _logger.Debug($"[UDP] Socket bound to port {Listener.Config.Port}");

        _isEnabled = false;
        _isRunning = false;
    }

    #endregion Constructor

    #region Public Methods

    public async System.Threading.Tasks.Task RunAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        if (!_isEnabled)
        {
            _logger.Warn("[UDP] Skipped receiving loop because UDP is disabled.");
            return;
        }
        else if (_isRunning)
        {
            _logger.Warn("[UDP] Receive loop is already running.");
            return;
        }

        _isRunning = true;

        _logger.Info("[UDP] {0} listening on port {1}", _protocol, Listener.Config.Port);

        using System.Buffers.IMemoryOwner<byte> memoryOwner = System.Buffers
            .MemoryPool<byte>.Shared.Rent(Listener.Config.BufferSize);

        System.Memory<byte> buffer = memoryOwner.Memory;

        System.Net.EndPoint remote = new System.Net.IPEndPoint(System.Net.IPAddress.Any, Listener.Config.Port);

        try
        {
            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    System.Net.Sockets.SocketReceiveFromResult result = await _listener.ReceiveFromAsync(
                        buffer, System.Net.Sockets.SocketFlags.None, remote, cancellationToken);

                    if (result.ReceivedBytes > Listener.Config.MinUdpSize)
                    {
                        _protocol.ProcessMessage(buffer.Span[..result.ReceivedBytes]);
                    }
                }
                catch (System.Net.Sockets.SocketException ex)
                {
                    _logger.Error("[UDP] Socket error (OpCode: {0}): {1}", ex.SocketErrorCode, ex.Message);
                    if (ex.SocketErrorCode == System.Net.Sockets.SocketError.Interrupted)
                    {
                        _logger.Info("[UDP] Listener on {0} interrupted", Listener.Config.Port);
                        this.Close();
                    }
                }
                catch (System.OperationCanceledException)
                {
                    _logger.Info("[UDP] Listener on {0} stopped", Listener.Config.Port);
                    this.Close();
                }
                catch (System.Exception ex)
                {
                    _logger.Error("[UDP] Unexpected error: {0}", ex);
                }
            }
        }
        finally
        {
            this.Close();
        }
    }

    public void Close()
    {
        if (!_isRunning)
            return;

        _isRunning = false;

        try
        {
            _listener.Close();
            _logger.Info("[UDP] Listener on port {0} has been stopped.", Listener.Config.Port);
        }
        catch (System.Exception ex)
        {
            _logger.Error("[UDP] Error while stopping UDP listener: {0}", ex);
        }
    }

    #endregion Public Methods
}