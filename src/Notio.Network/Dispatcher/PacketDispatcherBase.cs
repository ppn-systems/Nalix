namespace Notio.Network.Dispatcher;

/// <summary>
/// Serves as the base class for packet dispatchers, offering common configuration and logging support.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that implements both <see cref="Common.Package.IPacket"/> and <see cref="Common.Package.IPacketDeserializer{TPacket}"/>.
/// </typeparam>
public abstract class PacketDispatcherBase<TPacket> where TPacket : Common.Package.IPacket,
    Common.Package.IPacketEncryptor<TPacket>,
    Common.Package.IPacketCompressor<TPacket>,
    Common.Package.IPacketDeserializer<TPacket>
{
    /// <summary>
    /// Gets the logger instance associated with this dispatcher, if configured.
    /// </summary>
    protected Common.Logging.ILogger? Logger => Options.Logger;

    /// <summary>
    /// Gets the configuration options for this dispatcher instance.
    /// </summary>
    protected readonly Options.PacketDispatcherOptions<TPacket> Options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatcherBase{TPacket}"/> class
    /// using the provided <paramref name="options"/>.
    /// </summary>
    /// <param name="options">
    /// The dispatcher configuration options. Must not be <c>null</c>.
    /// </param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if <paramref name="options"/> is <c>null</c>.
    /// </exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    protected PacketDispatcherBase(Options.PacketDispatcherOptions<TPacket> options)
        => this.Options = options ?? throw new System.ArgumentNullException(nameof(options));

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatcherBase{TPacket}"/> class
    /// with optional configuration logic.
    /// </summary>
    /// <param name="configure">
    /// An optional delegate to configure the <see cref="Options.PacketDispatcherOptions{TPacket}"/> instance.
    /// </param>
    protected PacketDispatcherBase(System.Action<Options.PacketDispatcherOptions<TPacket>>? configure = null)
        : this(new Options.PacketDispatcherOptions<TPacket>()) => configure?.Invoke(this.Options);

    /// <summary>
    /// Executes the registered handler for a given packet and connection context asynchronously.
    /// </summary>
    /// <param name="handler">The delegate method responsible for processing the packet.</param>
    /// <param name="packet">The deserialized packet to be handled.</param>
    /// <param name="connection">The client connection that sent the packet.</param>
    /// <returns>A task that represents the asynchronous execution of the handler.</returns>
    protected static async System.Threading.Tasks.ValueTask ExecuteHandler(
        System.Func<TPacket, Common.Connection.IConnection, System.Threading.Tasks.Task> handler,
        TPacket packet,
        Common.Connection.IConnection connection)
        => await handler(packet, connection).ConfigureAwait(false);
}
