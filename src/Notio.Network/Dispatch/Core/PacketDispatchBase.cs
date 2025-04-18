namespace Notio.Network.Dispatch.Core;

/// <summary>
/// Serves as the base class for packet dispatchers, offering common configuration and logging support.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that implements both <see cref="Common.Package.IPacket"/> and <see cref="Common.Package.IPacketDeserializer{TPacket}"/>.
/// </typeparam>
public abstract class PacketDispatchBase<TPacket> where TPacket : Common.Package.IPacket,
    Common.Package.IPacketEncryptor<TPacket>,
    Common.Package.IPacketCompressor<TPacket>,
    Common.Package.IPacketDeserializer<TPacket>
{
    #region Properties

    /// <summary>
    /// Gets the logger instance associated with this dispatcher, if configured.
    /// </summary>
    protected Common.Logging.ILogger? Logger => Options.Logger;

    /// <summary>
    /// Gets the configuration options for this dispatcher instance.
    /// </summary>
    protected readonly Options.PacketDispatchOptions<TPacket> Options;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchBase{TPacket}"/> class
    /// using the provided <paramref name="options"/>.
    /// </summary>
    /// <param name="options">
    /// The dispatcher configuration options. Must not be <c>null</c>.
    /// </param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if <paramref name="options"/> is <c>null</c>.
    /// </exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    protected PacketDispatchBase(Options.PacketDispatchOptions<TPacket> options)
        => Options = options ?? throw new System.ArgumentNullException(nameof(options));

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchBase{TPacket}"/> class
    /// with optional configuration logic.
    /// </summary>
    /// <param name="configure">
    /// An optional delegate to configure the <see cref="Options.PacketDispatchOptions{TPacket}"/> instance.
    /// </param>
    protected PacketDispatchBase(System.Action<Options.PacketDispatchOptions<TPacket>>? configure = null)
        : this(new Options.PacketDispatchOptions<TPacket>()) => configure?.Invoke(Options);

    #endregion

    #region Ptotected Methods

    /// <summary>
    /// Executes the registered packet handler asynchronously using the provided packet and connection context.
    /// </summary>
    /// <param name="handler">
    /// A delegate that processes the packet. This delegate should implement the packet-specific logic,
    /// such as validation, response preparation, or triggering related workflows.
    /// </param>
    /// <param name="packet">
    /// The deserialized packet instance containing the data to be handled. Assumed to be already validated and routed.
    /// </param>
    /// <param name="connection">
    /// The connection instance representing the client that sent the packet. Provides context such as the remote address
    /// and any relevant session or authentication data.
    /// </param>
    /// <returns>
    /// A <see cref="System.Threading.Tasks.ValueTask"/> that represents the asynchronous execution of the handler logic.
    /// </returns>
    /// <remarks>
    /// This method is a thin wrapper around the provided delegate. It exists primarily to isolate the handler execution
    /// for logging, diagnostics, or future extensibility (e.g., execution hooks, cancellation, metrics).
    /// The call to the delegate is awaited with <c>ConfigureAwait(false)</c> to avoid context capture in asynchronous environments.
    /// </remarks>
    protected static async System.Threading.Tasks.ValueTask ExecuteHandler(
        System.Func<TPacket, Common.Connection.IConnection, System.Threading.Tasks.Task> handler,
        TPacket packet,
        Common.Connection.IConnection connection)
        => await handler(packet, connection).ConfigureAwait(false);

    /// <summary>
    /// Asynchronously processes a single incoming packet by resolving and executing the appropriate handler.
    /// </summary>
    /// <param name="packet">
    /// The packet to be processed. Must contain a valid <c>Id</c> to resolve a handler.
    /// </param>
    /// <param name="connection">
    /// The connection from which the packet was received. Provides context such as the remote endpoint.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation of handling the packet.
    /// </returns>
    /// <remarks>
    /// This method attempts to resolve a packet handler using the packet's <c>Id</c> via <c>Options.TryResolveHandler</c>.
    /// If a handler is found, it is invoked asynchronously with the provided <paramref name="packet"/> and 
    /// <paramref name="connection"/>. Any exceptions thrown by the handler are caught and logged as errors.
    /// If no handler is found, a warning is logged instead.
    /// </remarks>
    /// <exception cref="System.Exception">
    /// Exceptions thrown by the handler are caught and logged, but not rethrown. This prevents one faulty handler
    /// from crashing the dispatcher loop.
    /// </exception>
    protected async System.Threading.Tasks.Task ExecutePacketHandlerAsync(
        TPacket packet,
        Common.Connection.IConnection connection)
    {
        if (Options.TryResolveHandler(packet.Id,
            out System.Func<TPacket,
            Common.Connection.IConnection,
            System.Threading.Tasks.Task>? handler))
        {
            Logger?.Debug($"[Dispatch] Processing packet Id: {packet.Id} from {connection.RemoteEndPoint}...");

            try
            {
                await PacketDispatchBase<TPacket>
                    .ExecuteHandler(handler, packet, connection)
                    .ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                Logger?.Error(
                    $"[Dispatch] Exception occurred while handling packet Id: " +
                    $"{packet.Id} from {connection.RemoteEndPoint}. " +
                    $"Error: {ex.GetType().Name} - {ex.Message}", ex);
            }

            return;
        }

        Logger?.Warn($"[Dispatch] No handler found for packet Id: {packet.Id} from {connection.RemoteEndPoint}.");
    }

    #endregion
}
