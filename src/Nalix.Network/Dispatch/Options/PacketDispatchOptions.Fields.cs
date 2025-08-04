using Nalix.Common.Logging;
using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware.Core;

namespace Nalix.Network.Dispatch.Options;

/// <summary>
/// Provides options for packet dispatching, including middleware configuration,
/// error handling, and logging.
/// </summary>
/// <typeparam name="TPacket">The type of packet being dispatched.</typeparam>
public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket, IPacketTransformer<TPacket>
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
        this._handlerCache = [];
        this._pipeline = new PacketMiddlewarePipeline<TPacket>();
    }

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the logger instance used for logging within the packet dispatch options.
    /// </summary>
    public ILogger? Logger { get; private set; }

    #endregion Properties
}