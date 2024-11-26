// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Common.Packets.Abstractions;
using Nalix.Network.Dispatch.Delegates;
using Nalix.Network.Middleware;

namespace Nalix.Network.Dispatch.Options;

/// <summary>
/// Provides options for packet dispatching, including middleware configuration,
/// error handling, and logging.
/// </summary>
/// <typeparam name="TPacket">The type of packet being dispatched.</typeparam>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket
{
    #region Fields

    private readonly MiddlewarePipeline<TPacket> _pipeline;
    private readonly System.Collections.Generic.Dictionary<System.UInt16, PacketHandler<TPacket>> _handlerCache;

    /// <summary>
    /// Gets or sets a custom error-handling delegate invoked when packet processing fails.
    /// </summary>
    /// <remarks>
    /// If not set, exceptions are only logged. You can override this to trigger alerts or retries.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.AllowNull]
    private System.Action<System.Exception, System.UInt16> _errorHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchOptions{TPacket}"/> class.
    /// </summary>
    public PacketDispatchOptions()
    {
        _handlerCache = [];
        _pipeline = new MiddlewarePipeline<TPacket>();
    }

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the logger instance used for logging within the packet dispatch options.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public ILogger Logger { get; private set; }

    #endregion Properties
}
