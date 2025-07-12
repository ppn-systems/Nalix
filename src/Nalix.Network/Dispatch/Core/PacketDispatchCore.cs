using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Common.Package;
using Nalix.Network.Dispatch.Middleware;
using Nalix.Network.Dispatch.Middleware.Inbound;
using Nalix.Network.Dispatch.Options;

namespace Nalix.Network.Dispatch.Core;

/// <summary>
/// Serves as the base class for packet dispatchers, offering common configuration and logging support.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that implements both <see cref="IPacket"/> and <see cref="IPacketDeserializer{TPacket}"/>.
/// </typeparam>
public abstract class PacketDispatchCore<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>,
    IPacketDeserializer<TPacket>
{
    #region Properties

    /// <summary>
    /// Gets the logger instance associated with this dispatcher, if configured.
    /// </summary>
    protected ILogger? Logger => Options.Logger;

    /// <summary>
    /// Gets the configuration options for this dispatcher instance.
    /// </summary>
    protected readonly PacketDispatchOptions<TPacket> Options;

    /// <summary>
    /// The middleware pipeline used for processing packets.
    /// </summary>
    protected readonly PacketMiddlewarePipeline<TPacket> Pipeline;

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchCore{TPacket}"/> class
    /// using the provided <paramref name="options"/>.
    /// </summary>
    /// <param name="options">
    /// The dispatcher configuration options. Must not be <c>null</c>.
    /// </param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if <paramref name="options"/> is <c>null</c>.
    /// </exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0290:UsePre primary constructor", Justification = "<Pending>")]
    protected PacketDispatchCore(PacketDispatchOptions<TPacket> options)
    {
        Options = options ?? throw new System.ArgumentNullException(nameof(options));
        Pipeline = new PacketMiddlewarePipeline<TPacket>()
            .UsePre(new RateLimitMiddleware<TPacket>())
            .UsePre(new DecompressionMiddleware<TPacket>())
            .UsePre(new DecryptionMiddleware<TPacket>());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchCore{TPacket}"/> class
    /// with optional configuration logic.
    /// </summary>
    /// <param name="configure">
    /// An optional delegate to configure the <see cref="PacketDispatchOptions{TPacket}"/> instance.
    /// </param>
    protected PacketDispatchCore(System.Action<PacketDispatchOptions<TPacket>>? configure = null)
        : this(new PacketDispatchOptions<TPacket>()) => configure?.Invoke(Options);

    #endregion Constructors

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
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected async System.Threading.Tasks.ValueTask ExecuteHandlerAsync(
        TPacket packet,
        IConnection connection,
        System.Func<TPacket, IConnection, System.Threading.Tasks.Task> handler)
    {
        PacketContext<TPacket> context = new(packet, connection);

        async System.Threading.Tasks.Task HandlerWrapper()
        {
            await handler(context.Packet, context.Connection).ConfigureAwait(false);
        }

        await Pipeline.ExecuteAsync(context, HandlerWrapper).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously processes a single incoming packet by resolving and executing the appropriate handler.
    /// </summary>
    /// <param name="packet">
    /// The packet to be processed. Must contain a valid <c>OpCode</c> to resolve a handler.
    /// </param>
    /// <param name="connection">
    /// The connection from which the packet was received. Provides context such as the remote endpoint.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation of handling the packet.
    /// </returns>
    /// <remarks>
    /// This method attempts to resolve a packet handler using the packet's <c>OpCode</c> via <c>Options.TryResolveHandler</c>.
    /// If a handler is found, it is invoked asynchronously with the provided <paramref name="packet"/> and
    /// <paramref name="connection"/>. Any exceptions thrown by the handler are caught and logged as errors.
    /// If no handler is found, a warning is logged instead.
    /// </remarks>
    /// <exception cref="System.Exception">
    /// Exceptions thrown by the handler are caught and logged, but not rethrown. This prevents one faulty handler
    /// from crashing the dispatcher loop.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected async System.Threading.Tasks.Task ExecutePacketHandlerAsync(
        TPacket packet,
        IConnection connection)
    {
        if (Options.TryResolveHandler(packet.OpCode,
            out System.Func<TPacket,
            IConnection,
            System.Threading.Tasks.Task>? handler))
        {
            Logger?.Debug($"[Dispatch] Processing packet OpCode: {packet.OpCode} from {connection.RemoteEndPoint}...");

            try
            {
                await this.ExecuteHandlerAsync(packet, connection, handler)
                          .ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                Logger?.Error(
                    $"[Dispatch] Exception occurred while handling packet OpCode: " +
                    $"{packet.OpCode} from {connection.RemoteEndPoint}. " +
                    $"Error: {ex.GetType().Name} - {ex.Message}", ex);
            }

            return;
        }

        Logger?.Warn($"[Dispatch] No handler found for packet OpCode: {packet.OpCode} from {connection.RemoteEndPoint}.");
    }

    #endregion Ptotected Methods
}