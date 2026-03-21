// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Middleware;
using Nalix.Network.Middleware.Internal;
using Nalix.Network.Routing.Metadata;

namespace Nalix.Network.Routing.Options;

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
    /// Maps each registered opCode to the concrete packet type expected by its handler method.
    /// Populated automatically by <see cref="WithHandler{TController}(System.Func{TController})"/>.
    /// Used at dispatch time to validate that the deserialized packet's runtime type matches
    /// what the handler was compiled against — catching mismatches early with a clear error
    /// instead of a silent <see cref="System.InvalidCastException"/> inside the expression tree.
    /// </summary>
    /// <remarks>
    /// Key   = opCode (UInt16)<br/>
    /// Value = concrete <see cref="System.Type"/> that implements <typeparamref name="TPacket"/>,
    ///         e.g. <c>typeof(LoginPacket)</c>. The value is the first parameter type of the
    ///         handler method as reflected by <see cref="System.Reflection.ParameterInfo"/>.
    ///         For context-style handlers (<c>PacketContext&lt;TPacket&gt;</c>) the entry is
    ///         <see langword="null"/> — no concrete-type check is needed there.
    /// </remarks>
    private readonly System.Collections.Generic.Dictionary<System.UInt16, System.Type> _packetTypeMap;

    /// <summary>
    /// Network buffer middleware pipeline for processing raw byte buffers before packet transformation.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public readonly NetworkBufferMiddlewarePipeline NetworkPipeline;

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
        _packetTypeMap = [];
        _pipeline = new MiddlewarePipeline<TPacket>();

        NetworkPipeline = new NetworkBufferMiddlewarePipeline();

        // Add default network middleware for frame processing. You can customize this pipeline as needed.
        NetworkPipeline.Use(new FrameDecryptionMiddleware());
        NetworkPipeline.Use(new FrameDecompressMiddleware());
    }

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the logger instance used for logging within the packet dispatch options.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.AllowNull]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public ILogger Logging { get; private set; }

    #endregion Properties
}