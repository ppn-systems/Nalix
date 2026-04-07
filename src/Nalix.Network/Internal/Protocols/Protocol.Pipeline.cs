// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Framework.Injection;

namespace Nalix.Network.Internal.Protocols;

/// <summary>
/// Routes inbound connection frames through a fixed sequence of protocol stages:
/// Handshake (X25519) -> Decrypt -> Decompress -> Application.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="ProtocolPipeline"/> instance is associated with a single connection while it is bound.
/// The pipeline can be reused via pooling.
/// </para>
/// <para>
/// Pooling contract:
/// </para>
/// <list type="number">
/// <item><description>Rent or create an instance.</description></item>
/// <item><description>Call <see cref="Initialize(IConnection, IProtocol)"/>.</description></item>
/// <item><description>Call <see cref="Bind"/> to subscribe to <see cref="IConnection.OnProcessEvent"/>.</description></item>
/// <item><description>On connection close: call <see cref="Unbind"/> and then return the instance to the pool.</description></item>
/// </list>
/// <para>
/// Note:
/// <see cref="ResetForPool"/> is intended to be invoked by the pool implementation during <c>Return(...)</c>.
/// Callers should not normally call <see cref="ResetForPool"/> directly unless they are implementing a custom pool.
/// </para>
/// <para>
/// Ownership rule:
/// The pipeline owns disposal of <see cref="IConnectEventArgs"/> and its <see cref="IConnectEventArgs.Lease"/>.
/// Individual protocol stages must not dispose these objects.
/// </para>
/// <para>
/// Stage instances (<see cref="ProtocolX25519"/>, <see cref="ProtocolDecrypt"/>, <see cref="ProtocolDecompress"/>)
/// are resolved as shared singletons and must therefore remain stateless and thread-safe.
/// </para>
/// </remarks>
[DebuggerDisplay("Bound={_bound != 0}")]
internal sealed class ProtocolPipeline : IPoolable
{
    #region Static

    private static readonly IProtocolStage s_decrypt = InstanceManager.Instance.GetOrCreateInstance<ProtocolDecrypt>();
    private static readonly IProtocolStage s_handshake = InstanceManager.Instance.GetOrCreateInstance<ProtocolX25519>();
    private static readonly IProtocolStage s_decompress = InstanceManager.Instance.GetOrCreateInstance<ProtocolDecompress>();

    #endregion Static

    #region Fields

    private IProtocol? _application;
    private IConnection? _connection;

    private int _bound;

    #endregion Fields

    #region APIs

    /// <summary>
    /// Initializes this pipeline for a specific connection.
    /// </summary>
    /// <param name="connection">
    /// The connection that will own this pipeline while it is bound. The pipeline subscribes to
    /// <see cref="IConnection.OnProcessEvent"/> when <see cref="Bind"/> is called.
    /// </param>
    /// <param name="application">
    /// The final application protocol stage. This stage runs only after handshake is established and
    /// after decrypt/decompress have been applied (when applicable).
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="connection"/> or <paramref name="application"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the pipeline is currently bound. Call <see cref="Unbind"/> before reinitializing.
    /// </exception>
    /// <remarks>
    /// This method must be called before <see cref="Bind"/>. It is separate from construction to support pooling.
    /// </remarks>
    public void Initialize(IConnection connection, IProtocol application)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(application);

        // A pipeline instance must not be re-initialized while still bound.
        if (Volatile.Read(ref _bound) != 0)
        {
            throw new InvalidOperationException("ProtocolPipeline is already bound; call Unbind() before reinitializing.");
        }

        _connection = connection;
        _application = application;
    }

    /// <summary>
    /// Subscribes this pipeline to the connection's <see cref="IConnection.OnProcessEvent"/> event.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the pipeline has not been initialized, or if it is already bound.
    /// </exception>
    /// <remarks>
    /// Callers must ensure <see cref="Initialize(IConnection, IProtocol)"/> has been called before binding.
    /// Binding twice is treated as a programming error because it would double-subscribe.
    /// </remarks>
    [DebuggerStepThrough]
    public void Bind()
    {
        if (_connection is null || _application is null)
        {
            throw new InvalidOperationException("ProtocolPipeline must be initialized before binding.");
        }

        if (Interlocked.CompareExchange(ref _bound, 1, 0) != 0)
        {
            throw new InvalidOperationException("ProtocolPipeline is already bound.");
        }

        _connection.OnProcessEvent += this.ProcessMessage;
    }

    /// <summary>
    /// Unsubscribes this pipeline from the connection's <see cref="IConnection.OnProcessEvent"/> event.
    /// </summary>
    /// <remarks>
    /// This method is safe to call multiple times. It is typically invoked during connection teardown
    /// to prevent memory leaks and duplicate callbacks.
    /// </remarks>
    [DebuggerStepThrough]
    public void Unbind()
    {
        IConnection? connection = _connection;
        if (connection is null)
        {
            _ = Interlocked.Exchange(ref _bound, 0);
            return;
        }

        // Ensure we only unbind once.
        if (Interlocked.Exchange(ref _bound, 0) == 0)
        {
            return;
        }

        connection.OnProcessEvent -= this.ProcessMessage;
    }

    /// <summary>
    /// Processes an inbound frame by running it through the pipeline stages.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="args">The event arguments that contain the connection and payload lease.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Stage behavior:
    /// </para>
    /// <list type="bullet">
    /// <item><description>If handshake is not established, only the handshake stage runs.</description></item>
    /// <item><description>After handshake establishment, decrypt and decompress stages run (when applicable), then the application stage runs.</description></item>
    /// </list>
    /// <para>
    /// Disposal:
    /// This method always disposes <paramref name="args"/> and its <see cref="IConnectEventArgs.Lease"/> in a <c>finally</c> block.
    /// Individual stages must not dispose them.
    /// </para>
    /// </remarks>
    public void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // Local copies reduce race risk if ResetForPool is called incorrectly.
        IProtocol? application = _application;

        if (application is null)
        {
            // Not initialized or already reset; drop safely.
            args.Lease?.Dispose();
            args.Dispose();
            return;
        }

        try
        {
            // Stage 1: Handshake gate. Only handshake frames are processed until established.
            if (!ProtocolX25519.IsEstablished(args.Connection))
            {
                s_handshake.ProcessMessage(sender, args);
                return;
            }

            // Stage 2: Decrypt stage.
            s_decrypt.ProcessMessage(sender, args);

            // Stage 3: Decompress stage.
            s_decompress.ProcessMessage(sender, args);

            // Stage 4: Application stage.
            application.ProcessMessage(sender, args);
        }
        catch (CipherException ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(ProtocolPipeline)}:{nameof(ProcessMessage)}] {ex.Message}");
        }
        catch (InvalidCastException ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(ProtocolPipeline)}:{nameof(ProcessMessage)}] {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(ProtocolPipeline)}:{nameof(ProcessMessage)}] {ex.Message}");
        }
        catch (SerializationFailureException ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(ProtocolPipeline)}:{nameof(ProcessMessage)}] {ex.Message}");
        }
        catch (Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error(ex, $"[{nameof(ProtocolPipeline)}:{nameof(ProcessMessage)}] Unhandled exception during message processing.");
        }
        finally
        {
            // Ownership rule: pipeline disposes args/lease exactly once.
            args.Lease?.Dispose();
            args.Dispose();
        }
    }

    /// <summary>
    /// Resets this instance so it can be reused by an object pool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method clears references to the connection and application protocol to avoid retaining the connection object graph.
    /// </para>
    /// <para>
    /// This method is typically called by the pool implementation as part of returning an object to the pool.
    /// Most callers should call <c>pool.Return(pipeline)</c> and not invoke <see cref="ResetForPool"/> directly.
    /// </para>
    /// </remarks>
    [DebuggerStepThrough]
    public void ResetForPool()
    {
        // If still bound, unbind defensively.
        this.Unbind();

        _connection = null;
        _application = null;

        // Reset flags so the pooled instance can be re-used.
        _ = Interlocked.Exchange(ref _bound, 0);
    }

    #endregion APIs
}
