// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;

namespace Nalix.Network.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IConnectionHub"/>.
/// </summary>
public static class ConnectionHubExtensions
{


    /// <summary>
    /// Multicasts a pre-serialized message buffer to a specific collection of connections.
    /// </summary>
    /// <param name="hub">The connection hub.</param>
    /// <param name="connections">The read-only collection of connections to receive the message.</param>
    /// <param name="message">The pre-serialized message buffer to multicast.</param>
    /// <param name="transport">The network transport protocol to use.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous multicast operation.</returns>
    [Obsolete(
        "This overload sends a pre-serialized buffer and bypasses the normal compression and encryption pipeline. Use the packet-based multicast overload instead.",
        error: false,
        DiagnosticId = "NALIX_NET001")]
    public static async Task MulticastAsync(
        this IConnectionHub hub,
        IReadOnlyCollection<IConnection> connections,
        ReadOnlyMemory<byte> message,
        NetworkTransport transport = NetworkTransport.TCP,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(connections);

        int connectionCount = connections.Count;
        if (connectionCount == 0 || message.IsEmpty)
        {
            return;
        }

        Task[]? tasks = null;
        IConnection[]? owners = null;
        int taskCount = 0;

        try
        {
            if (connections is IReadOnlyList<IConnection> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    QueueSend(
                        list[i],
                        connectionCount,
                        message,
                        transport,
                        cancellationToken,
                        ref tasks,
                        ref owners,
                        ref taskCount);
                }
            }
            else
            {
                foreach (IConnection connection in connections)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    QueueSend(
                        connection,
                        connectionCount,
                        message,
                        transport,
                        cancellationToken,
                        ref tasks,
                        ref owners,
                        ref taskCount);
                }
            }

            if (taskCount == 0 || tasks is null || owners is null)
            {
                return;
            }

            try
            {
                await Task.WhenAll(tasks.AsSpan(0, taskCount)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                {
                    DiagnosticsEvents.Write(
                        DiagnosticsEvents.Internal.Debug,
                        new DiagnosticLog("NW.ConnectionHubExtensions:MulticastAsync", "multicast-cancel"));
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                LogMulticastFailures(tasks, owners, taskCount);
            }
        }
        finally
        {
            if (tasks is not null)
            {
                Array.Clear(tasks, 0, taskCount);
                ArrayPool<Task>.Shared.Return(tasks);
            }

            if (owners is not null)
            {
                Array.Clear(owners, 0, taskCount);
                ArrayPool<IConnection>.Shared.Return(owners);
            }
        }
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void QueueSend(
        IConnection connection,
        int connectionCount,
        ReadOnlyMemory<byte> message,
        NetworkTransport transport,
        CancellationToken cancellationToken,
        ref Task[]? tasks,
        ref IConnection[]? owners,
        ref int taskCount)
    {
        if (connection.IsDisposed)
        {
            return;
        }

        IConnection.ITransport? targetTransport =
            transport == NetworkTransport.UDP ? connection.UDP : connection.TCP;

        if (targetTransport is null)
        {
            return;
        }

        try
        {
            ValueTask sendTask = targetTransport.SendAsync(message, cancellationToken);
            if (sendTask.IsCompletedSuccessfully)
            {
                return;
            }

            tasks ??= ArrayPool<Task>.Shared.Rent(connectionCount);
            owners ??= ArrayPool<IConnection>.Shared.Rent(connectionCount);

            tasks[taskCount] = sendTask.AsTask();
            owners[taskCount] = connection;
            taskCount++;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
            {
                DiagnosticsEvents.Write(
                    DiagnosticsEvents.Internal.Error,
                    new DiagnosticLog("NW.ConnectionHubExtensions:MulticastAsync", $"send-failure id={connection.ID:X16}", ex));
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LogMulticastFailures(
        Task[] tasks,
        IConnection[] owners,
        int taskCount)
    {
        for (int i = 0; i < taskCount; i++)
        {
            Task task = tasks[i];
            if (!task.IsFaulted)
            {
                continue;
            }

            Exception? exception = task.Exception?.GetBaseException();
            if (exception is null || !ExceptionClassifier.IsNonFatal(exception))
            {
                continue;
            }

            IConnection owner = owners[i];
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
            {
                DiagnosticsEvents.Write(
                    DiagnosticsEvents.Internal.Error,
                    new DiagnosticLog("NW.ConnectionHubExtensions:MulticastAsync", $"send-failure id={owner.ID:X16}", exception));
            }
        }
    }
}
