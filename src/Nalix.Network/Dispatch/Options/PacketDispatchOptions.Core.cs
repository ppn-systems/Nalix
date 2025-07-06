using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Common.Package;
using Nalix.Network.Configurations;
using Nalix.Network.Security.Guard;

namespace Nalix.Network.Dispatch.Options;

/// <summary>
/// Provides configurable options for <see cref="PacketDispatch{TPacket}"/> behavior and lifecycle.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type this dispatcher handles. Must implement <see cref="IPacket"/>.
/// </typeparam>
/// <remarks>
/// Use this class to register packet handlers, enable compression/encryption, configure logging,
/// and define custom error-handling or metrics tracking logic.
/// </remarks>
public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>
{
    #region Constants

    private const System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes RequiredMembers =
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor;

    #endregion Constants

    #region Fields

    private ILogger? _logger;
    private readonly PacketRateLimitGuard _rateLimiter;

    /// <summary>
    /// Gets or sets the callback used to report the execution time of packet handlers.
    /// </summary>
    /// <remarks>
    /// This is invoked after each packet is processed, passing the handler name and time taken (ms).
    /// </remarks>
    private bool _isMetricsEnabled;

    /// <summary>
    /// Callback function to collect execution time metrics for packet processing.
    /// </summary>
    /// <remarks>
    /// The callback receives the packet handler name and execution time in milliseconds.
    /// </remarks>
    private System.Action<string, long>? _metricsCallback;

    /// <summary>
    /// Gets or sets a custom error-handling delegate invoked when packet processing fails.
    /// </summary>
    /// <remarks>
    /// If not set, exceptions are only logged. You can override this to trigger alerts or retries.
    /// </remarks>
    private System.Action<System.Exception, ushort>? _errorHandler;

    /// <summary>
    /// A dictionary mapping packet command IDs (ushort) to their respective handlers.
    /// </summary>
    private readonly System.Collections.Generic.Dictionary
        <ushort, System.Func<TPacket, IConnection, System.Threading.Tasks.Task>> _handlers = [];

    #endregion Fields

    #region Properties

    /// <summary>
    /// The logger instance used for logging.
    /// </summary>
    /// <remarks>
    /// If not configured, logging may be disabled.
    /// </remarks>
    public ILogger? Logger => _logger;

    /// <summary>
    /// Configuration options for ChannelDispatch
    /// </summary>
    public DispatchQueueOptions QueueOptions { get; set; } = new DispatchQueueOptions();

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchOptions{TPacket}"/> class with default values.
    /// </summary>
    /// <remarks>
    /// This constructor sets up the packet handler map and allows subsequent fluent configuration.
    /// </remarks>
    public PacketDispatchOptions() => _rateLimiter = new PacketRateLimitGuard();

    #endregion Constructors
}
