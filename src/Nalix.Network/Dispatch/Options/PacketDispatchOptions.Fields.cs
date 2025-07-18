using Nalix.Common.Logging;
using Nalix.Common.Package;
using Nalix.Network.Configurations;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware.Core;
using Nalix.Shared.Configuration;

namespace Nalix.Network.Dispatch.Options;

/// <summary>
/// Provides options for packet dispatching, including middleware configuration,
/// error handling, and logging.
/// </summary>
/// <typeparam name="TPacket">The type of packet being dispatched.</typeparam>
public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>
{
    #region Fields

    private ILogger? _logger;

    private readonly PacketMiddlewarePipeline<TPacket> _pipeline;

    private readonly System.Collections.Generic.Dictionary<
        System.UInt16, PacketHandlerDelegate<TPacket>> _handlerCache;

    /// <summary>
    /// Gets or sets a custom error-handling delegate invoked when packet processing fails.
    /// </summary>
    /// <remarks>
    /// If not set, exceptions are only logged. You can override this to trigger alerts or retries.
    /// </remarks>
    private System.Action<System.Exception, System.UInt16>? _errorHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchOptions{TPacket}"/> class.
    /// </summary>
    public PacketDispatchOptions()
    {
        _handlerCache = [];
        _pipeline = new PacketMiddlewarePipeline<TPacket>();
    }

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the logger instance used for logging within the packet dispatch options.
    /// </summary>
    public ILogger? Logger => _logger;

    /// <summary>
    /// Gets the dispatch queue options.
    /// </summary>
    public readonly DispatchQueueOptions QueueOptions = ConfigurationStore.Instance.Get<DispatchQueueOptions>();

    #endregion Properties
}