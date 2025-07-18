using Nalix.Common.Logging;
using Nalix.Network.Configurations;
using Nalix.Network.Protocols;

namespace Nalix.Network.Listeners.Udp;

internal class UdpIngressWorker
{
    #region Fields

    private readonly ILogger _logger;
    private readonly IProtocol _protocol;
    private readonly System.Net.Sockets.Socket _listener;

    private volatile System.Boolean _isEnabled;
    private volatile System.Boolean _isRunning;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current state of the UDP listener.
    /// </summary>
    public System.Boolean IsRunning => this._isRunning;

    /// <summary>
    /// Enables or disables the UDP receive loop.
    /// </summary>
    public System.Boolean IsEnabled
    {
        get => this._isEnabled;
        set
        {
            if (this._isRunning)
            {
                throw new System.InvalidOperationException("Cannot change IsUdpEnabled while listening.");
            }

            this._isEnabled = value;
        }
    }

    #endregion Properties

    #region Constructor

    public UdpIngressWorker(ILogger logger, IProtocol protocol)
    {
        this._logger = logger;
        this._protocol = protocol;

        this._listener = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Dgram,
            System.Net.Sockets.ProtocolType.Udp)
        {
            ExclusiveAddressUse = !Listener.Config.ReuseAddress
        };

        this._listener.SetSocketOption(
            System.Net.Sockets.SocketOptionLevel.Socket,
            System.Net.Sockets.SocketOptionName.ReuseAddress,
            Listener.Config.ReuseAddress ? SocketSettings.True : SocketSettings.False);

        this._listener.Bind(
            new System.Net.IPEndPoint(System.Net.IPAddress.Any, Listener.Config.Port));
        this._logger.Debug($"[UDP] Socket bound to port {Listener.Config.Port}");

        this._isEnabled = false;
        this._isRunning = false;
    }

    #endregion Constructor

    #region Public Methods

    public async System.Threading.Tasks.Task RunAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        if (!this._isEnabled)
        {
            this._logger.Warn("[UDP] Skipped receiving loop because UDP is disabled.");
            return;
        }
        else if (this._isRunning)
        {
            this._logger.Warn("[UDP] Receive loop is already running.");
            return;
        }

        this._isRunning = true;

        this._logger.Info("[UDP] {0} listening on port {1}", this._protocol, Listener.Config.Port);

        using System.Buffers.IMemoryOwner<System.Byte> memoryOwner = System.Buffers
            .MemoryPool<System.Byte>.Shared.Rent(Listener.Config.BufferSize);

        System.Memory<System.Byte> buffer = memoryOwner.Memory;

        System.Net.EndPoint remote = new System.Net.IPEndPoint(System.Net.IPAddress.Any, Listener.Config.Port);

        try
        {
            while (this._isRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    System.Net.Sockets.SocketReceiveFromResult result = await this._listener.ReceiveFromAsync(
                        buffer, System.Net.Sockets.SocketFlags.None, remote, cancellationToken);

                    if (result.ReceivedBytes > Listener.Config.MinUdpSize)
                    {
                        this._protocol.ProcessMessage(buffer.Span[..result.ReceivedBytes]);
                    }
                }
                catch (System.Net.Sockets.SocketException ex)
                {
                    this._logger.Error("[UDP] Socket error (OpCode: {0}): {1}", ex.SocketErrorCode, ex.Message);
                    if (ex.SocketErrorCode == System.Net.Sockets.SocketError.Interrupted)
                    {
                        this._logger.Info("[UDP] Listener on {0} interrupted", Listener.Config.Port);
                        this.Close();
                    }
                }
                catch (System.OperationCanceledException)
                {
                    this._logger.Info("[UDP] Listener on {0} stopped", Listener.Config.Port);
                    this.Close();
                }
                catch (System.Exception ex)
                {
                    this._logger.Error("[UDP] Unexpected error: {0}", ex);
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
        if (!this._isRunning)
        {
            return;
        }

        this._isRunning = false;

        try
        {
            this._listener.Close();
            this._logger.Info("[UDP] Listener on port {0} has been stopped.", Listener.Config.Port);
        }
        catch (System.Exception ex)
        {
            this._logger.Error("[UDP] Error while stopping UDP listener: {0}", ex);
        }
    }

    #endregion Public Methods
}