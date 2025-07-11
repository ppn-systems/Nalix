using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Common.Package;
using Nalix.Network.Configurations;
using Nalix.Shared.Configuration;

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

    private const System.Reflection.BindingFlags BindingFlags =
              System.Reflection.BindingFlags.Public |
              System.Reflection.BindingFlags.Instance |
              System.Reflection.BindingFlags.Static;

    private const System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes PublicMethods =
                  System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods;

    #endregion Constants

    #region Fields

    private ILogger? _logger;

    /// <summary>
    /// Gets or sets a custom error-handling delegate invoked when packet processing fails.
    /// </summary>
    /// <remarks>
    /// If not set, exceptions are only logged. You can override this to trigger alerts or retries.
    /// </remarks>
    private System.Action<System.Exception, System.UInt16>? _errorHandler;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.Reflection.MethodInfo, PacketDescriptor> _attributeCache = new();

    private readonly System.Collections.Frozen.FrozenDictionary<
        System.Type,
        System.Func<System.Object?, TPacket, IConnection, System.Threading.Tasks.Task>> _typeCache;

    /// <summary>
    /// A dictionary mapping packet command IDs (ushort) to their respective handlers.
    /// </summary>
    private readonly System.Collections.Generic.Dictionary
        <System.UInt16, System.Func<TPacket, IConnection, System.Threading.Tasks.Task>> _handlerCache = [];

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
    /// Configuration options for MultiLevelQueue
    /// </summary>
    public DispatchQueueOptions QueueOptions { get; } = ConfigurationStore.Instance.Get<DispatchQueueOptions>();

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchOptions{TPacket}"/> class with default values.
    /// </summary>
    /// <remarks>
    /// This constructor sets up the packet handler map and allows subsequent fluent configuration.
    /// </remarks>
    public PacketDispatchOptions()
    {
        _typeCache = CreateHandlerLookup();
    }

    #endregion Constructors
}