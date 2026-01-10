// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Middleware;
using Nalix.Network.Middleware.Internal;
using Nalix.Network.Routing.Metadata;

namespace Nalix.Network.Routing;

/// <summary>
/// Provides options for packet dispatching, including middleware configuration,
/// error handling, and logging.
/// </summary>
/// <typeparam name="TPacket">The type of packet being dispatched.</typeparam>
[DebuggerNonUserCode]
[SkipLocalsInit]
public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket
{
    #region Fields

    private readonly MiddlewarePipeline<TPacket> _pipeline;
    private readonly Dictionary<ushort, PacketHandler<TPacket>> _handlerCache;

    /// <summary>
    /// Maps each registered opCode to the concrete packet type expected by its handler method.
    /// Populated automatically by <see cref="WithHandler{TController}(Func{TController})"/>.
    /// Used at dispatch time to validate that the deserialized packet's runtime type matches
    /// what the handler was compiled against — catching mismatches early with a clear error
    /// instead of a silent <see cref="InvalidCastException"/> inside the expression tree.
    /// </summary>
    /// <remarks>
    /// Key   = opCode (UInt16)<br/>
    /// Value = concrete <see cref="Type"/> that implements <typeparamref name="TPacket"/>,
    ///         e.g. <c>typeof(LoginPacket)</c>. The value is the first parameter type of the
    ///         handler method as reflected by <see cref="ParameterInfo"/>.
    ///         For context-style handlers (<c>PacketContext&lt;TPacket&gt;</c>) the entry is
    ///         <see langword="null"/> — no concrete-type check is needed there.
    /// </remarks>
    private readonly Dictionary<ushort, Type?> _packetTypeMap;

    /// <summary>
    /// Network buffer middleware pipeline for processing raw byte buffers before packet transformation.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public NetworkBufferMiddlewarePipeline NetworkPipeline { get; }

    /// <summary>
    /// Gets or sets a custom error-handling delegate invoked when packet processing fails.
    /// </summary>
    /// <remarks>
    /// If not set, exceptions are only logged. You can override this to trigger alerts or retries.
    /// </remarks>
    private Action<Exception, ushort>? _errorHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchOptions{TPacket}"/> class.
    /// </summary>
    public PacketDispatchOptions()
    {
        _handlerCache = [];
        _packetTypeMap = [];
        _pipeline = new MiddlewarePipeline<TPacket>();

        this.NetworkPipeline = new NetworkBufferMiddlewarePipeline();

        // Add default network middleware for frame processing. You can customize this pipeline as needed.
        this.NetworkPipeline.Use(new FrameDecryptionMiddleware());
        this.NetworkPipeline.Use(new FrameDecompressMiddleware());
    }

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the logger instance used for logging within the packet dispatch options.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ILogger? Logging { get; private set; }

    /// <summary>
    /// Specifies how many dispatch loops the <see cref="PacketDispatchChannel"/> should start.
    /// When <see langword="null"/>, the dispatcher chooses <c>Math.Clamp(Environment.ProcessorCount / 2, 1, 12)</c>.
    /// </summary>
    public int? DispatchLoopCount { get; private set; }

    #endregion Properties
}
