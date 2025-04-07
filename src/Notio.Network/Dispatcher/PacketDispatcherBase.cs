namespace Notio.Network.Dispatcher;

/// <summary>
/// Serves as the base class for packet dispatchers, offering common configuration and logging support.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that implements both <see cref="Common.Package.IPacket"/> and <see cref="Common.Package.IPacketDeserializer{TPacket}"/>.
/// </typeparam>
public abstract class PacketDispatcherBase<TPacket>
    where TPacket : Common.Package.IPacket, Common.Package.IPacketDeserializer<TPacket>
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
}
