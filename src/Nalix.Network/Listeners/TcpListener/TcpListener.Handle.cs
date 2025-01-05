// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Enums;
using Nalix.Common.Exceptions;
using Nalix.Common.Logging;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Network.Internal;
using Nalix.Network.Internal.Pooled;
using Nalix.Network.Throttling;
using Nalix.Network.Timing;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Listeners.Tcp;

public abstract partial class TcpListenerBase
{
    [System.Diagnostics.DebuggerStepThrough]
    private void ProcessConnection(
        [System.Diagnostics.CodeAnalysis.NotNull] IConnection connection)
    {
        try
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(ProcessConnection)}] new={connection.EndPoint}");

            _protocol.OnAccept(connection, _cancellationToken);
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[NW.{nameof(TcpListenerBase)}:{nameof(ProcessConnection)}] process-error={connection.EndPoint} ex={ex.Message}");
            connection.Close();
        }
    }

    [System.Diagnostics.DebuggerStepThrough]
    private void HandleConnectionClose(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object sender,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnectEventArgs args)
    {
        if (args?.Connection == null)
        {
            return;
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleConnectionClose)}] close={args.Connection.EndPoint}");

        // De-subscribe to prevent memory leaks
        args.Connection.OnCloseEvent -= this.HandleConnectionClose;
        args.Connection.OnProcessEvent -= _protocol.ProcessMessage;
        args.Connection.OnPostProcessEvent -= _protocol.PostProcessMessage;
        args.Connection.OnCloseEvent -= InstanceManager.Instance.GetOrCreateInstance<ConnectionLimiter>()
                                                                .OnConnectionClosed;

        args.Connection.Dispose();
    }

    [System.Diagnostics.DebuggerStepThrough]
    private IConnection InitializeConnection(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.Sockets.Socket socket,
        [System.Diagnostics.CodeAnalysis.NotNull] PooledAcceptContext context)
    {
        InitializeOptions(socket);

        try
        {
            IConnection connection = new Connections.Connection(socket);

            connection.OnCloseEvent += this.HandleConnectionClose;

            connection.OnProcessEvent += _protocol.ProcessMessage;
            connection.OnPostProcessEvent += _protocol.PostProcessMessage;
            connection.OnCloseEvent += InstanceManager.Instance.GetOrCreateInstance<ConnectionLimiter>()
                                                               .OnConnectionClosed;

            if (Config.EnableTimeout)
            {
                InstanceManager.Instance.GetOrCreateInstance<TimingWheel>()
                                        .Register(connection);
            }

            return connection;
        }
        finally
        {
            // Ensure the context is returned to the pool even if connection creation fails
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return<PooledAcceptContext>(context);
        }
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void SafeCloseSocket(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.Sockets.Socket socket)
    {
        try
        {
            socket?.Close();
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] accept-error ex={ex.Message}");
        }
    }

    [System.Diagnostics.DebuggerStepThrough]
    private void HandleAccept(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.Sockets.SocketAsyncEventArgs args)
    {
        try
        {
            if (args.SocketError == System.Net.Sockets.SocketError.Success &&
           args.AcceptSocket is System.Net.Sockets.Socket socket)
            {
                try
                {
                    if (!socket.Connected || socket.Handle.ToInt64() == -1)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] invalid-socket remote={socket.RemoteEndPoint}");

                        SafeCloseSocket(socket);
                        return;
                    }

                    if (!InstanceManager.Instance.GetOrCreateInstance<ConnectionLimiter>()
                                                 .IsConnectionAllowed(socket.RemoteEndPoint))
                    {
                        SafeCloseSocket(socket);
                        return;
                    }

                    // Create and process connection similar to async version
                    PooledAcceptContext context = ((PooledSocketAsyncEventArgs)args).Context!;
                    IConnection connection = this.InitializeConnection(socket, context);

                    // Process the connection
                    _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().StartWorker(
                        name: NetTaskCatalog.TcpProcessWorker(_port, connection.ID.ToString()),
                        group: NetTaskCatalog.TcpProcessGroup(_port),
                        work: async (_, _) =>
                        {
                            ProcessConnection(connection);
                            await System.Threading.Tasks.Task.CompletedTask;
                        },
                        options: new WorkerOptions
                        {
                            RetainFor = System.TimeSpan.Zero,
                            IdType = SnowflakeType.System,
                            Tag = NetTaskCatalog.Segments.Net
                        }
                    );

                    // Rebind a fresh context for the next accept on this args
                    PooledAcceptContext nextCtx = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                          .Get<PooledAcceptContext>();
                    ((PooledSocketAsyncEventArgs)args).Context = nextCtx;
                    nextCtx.BindArgsForSync((PooledSocketAsyncEventArgs)args);
                }
                catch (System.ObjectDisposedException)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] disposed-during-accept remote={socket.RemoteEndPoint}");

                    SafeCloseSocket(socket);
                    if (args is PooledSocketAsyncEventArgs pooled && pooled.Context != null)
                    {
                        InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                .Return<PooledAcceptContext>(pooled.Context);

                        // Rebind a fresh context for next accepts on this args
                        PooledAcceptContext newCtx = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                             .Get<PooledAcceptContext>();
                        pooled.Context = newCtx;
                        newCtx.BindArgsForSync(pooled);
                    }
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] accept-error ex={ex.Message}");

                    try
                    {
                        socket.Close();
                    }
                    catch { }
                    InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                            .Return<PooledAcceptContext>(((PooledSocketAsyncEventArgs)args).Context!);

                    PooledAcceptContext newCtx = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                         .Get<PooledAcceptContext>();
                    ((PooledSocketAsyncEventArgs)args).Context = newCtx;
                    newCtx.BindArgsForSync((PooledSocketAsyncEventArgs)args);
                }
            }
            else
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] accept-failed={args.SocketError}");

                if (args is PooledSocketAsyncEventArgs pooled)
                {
                    ObjectPoolManager pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();
                    PooledAcceptContext oldCtx = pooled.Context;
                    if (oldCtx is not null)
                    {
                        pool.Return<PooledAcceptContext>(oldCtx);
                    }

                    PooledAcceptContext newCtx = pool.Get<PooledAcceptContext>();
                    pooled.Context = newCtx;
                    newCtx.BindArgsForSync(pooled);
                }
            }
        }
        finally
        {
            // Always clear to make the args reusable safely
            args.AcceptSocket = null;
        }
    }

    [System.Diagnostics.DebuggerStepThrough]
    private void OnSyncAcceptCompleted(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object sender,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.Sockets.SocketAsyncEventArgs args)
    {
        try
        {
            this.HandleAccept(args);
        }
        finally
        {
            // Unsubscribe before returning to pool to prevent duplicate callbacks
            args.Completed -= this.OnSyncAcceptCompleted;

            // Ensure the args is clean before returning to pool
            args.AcceptSocket = null;
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return((PooledSocketAsyncEventArgs)args);
        }

        PooledAcceptContext context = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                              .Get<PooledAcceptContext>();

        PooledSocketAsyncEventArgs newArgs = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                     .Get<PooledSocketAsyncEventArgs>();

        newArgs.Context = context;
        context.BindArgsForSync(newArgs);

        newArgs.Completed += this.OnSyncAcceptCompleted;

        this.AcceptNext(newArgs, _cancellationToken);
    }

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void AcceptNext(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.Sockets.SocketAsyncEventArgs args,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Take a stable local copy to reduce races
            System.Net.Sockets.Socket s = System.Threading.Volatile.Read(ref _listener);
            if (s?.IsBound != true)
            {
                break;
            }

            // Re-arm args before each use
            args.AcceptSocket = null;

            try
            {
                // Async path: will continue in Completed handler
                if (s.AcceptAsync(args))
                {
                    break;
                }

                // Sync completion
                HandleAccept(args);
            }
            catch (System.ObjectDisposedException)
            {
                // Listener closed during/just before AcceptAsync
                break;
            }
            catch (System.Net.Sockets.SocketException ex) when (
                ex.SocketErrorCode is
                System.Net.Sockets.SocketError.Interrupted or
                System.Net.Sockets.SocketError.OperationAborted or
                System.Net.Sockets.SocketError.ConnectionAborted)
            {
                // Expected during shutdown
                break;
            }
            catch (System.Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptNext)}] accept-error ex={ex.Message}");

                // Brief delay to prevent CPU spinning on repeated errors
                System.Threading.Tasks.Task.Delay(50, System.Threading.CancellationToken.None)
                                           .GetAwaiter()
                                           .GetResult();
            }
            finally
            {
                // Ensure args reusable
                args.AcceptSocket = null;
            }
        }
    }

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private async System.Threading.Tasks.Task AcceptConnectionsAsync(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                IConnection connection = await this.CreateConnectionAsync(cancellationToken)
                                                   .ConfigureAwait(false);

                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().StartWorker(
                    name: NetTaskCatalog.TcpProcessWorker(_port, connection.ID.ToString()),
                    group: NetTaskCatalog.TcpProcessGroup(_port),
                    work: async (_, _) =>
                    {
                        ProcessConnection(connection);
                        await System.Threading.Tasks.Task.CompletedTask;
                    },
                    options: new WorkerOptions
                    {
                        RetainFor = System.TimeSpan.Zero,
                        IdType = SnowflakeType.System,
                        Tag = NetTaskCatalog.Segments.Net
                    }
                );

                continue;
            }
            catch (InternalErrorException)
            {
                await System.Threading.Tasks.Task.Delay(10, System.Threading.CancellationToken.None)
                                                 .ConfigureAwait(false);
                continue;
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break; // Exit loop on cancellation
            }
            catch (System.Net.Sockets.SocketException ex) when (IsIgnorableAcceptError(ex.SocketErrorCode, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested || State != ListenerState.Running)
                {
                    break;
                }

                // Transient: Gentle backoff to avoid spam
                await System.Threading.Tasks.Task.Delay(50, System.Threading.CancellationToken.None)
                                                 .ConfigureAwait(false);
                continue;
            }
            catch (System.Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] accept-error ex={ex.Message}");

                // Brief delay to prevent CPU spinning on repeated errors
                await System.Threading.Tasks.Task.Delay(50, cancellationToken)
                                                 .ConfigureAwait(false);
            }
        }
    }

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    private async System.Threading.Tasks.ValueTask<IConnection> CreateConnectionAsync(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PooledAcceptContext context = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                              .Get<PooledAcceptContext>();

        try
        {
            System.Net.Sockets.Socket socket;

            if (_listener == null)
            {
                throw new System.InvalidOperationException(
                    $"[NW.{nameof(TcpListenerBase)}:{nameof(CreateConnectionAsync)}] socket is not initialized.");
            }

            // Wait async accept:
            socket = await context.BeginAcceptAsync(_listener)
                                  .ConfigureAwait(false);

            if (!InstanceManager.Instance.GetOrCreateInstance<ConnectionLimiter>()
                                         .IsConnectionAllowed(socket.RemoteEndPoint))
            {
                SafeCloseSocket(socket);

                throw new InternalErrorException();
            }

            return this.InitializeConnection(socket, context);
        }
        catch
        {
            // Don't forget to return to pool in case of failure
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return<PooledAcceptContext>(context);

            throw new InternalErrorException();
        }
    }
}