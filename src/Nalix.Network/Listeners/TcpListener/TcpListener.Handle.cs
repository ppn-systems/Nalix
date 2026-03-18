// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Pooled;
using Nalix.Network.Timekeeping;

namespace Nalix.Network.Listeners.Tcp;

public abstract partial class TcpListenerBase
{
    [System.Diagnostics.DebuggerStepThrough]
    private void ProcessConnection(
        [System.Diagnostics.CodeAnalysis.NotNull] IConnection connection)
    {
        try
        {
            _protocol.OnAccept(connection, _cancellationToken);

            _metrics.RECORD_ACCEPTED();
            s_logger?.Trace($"[NW.{nameof(TcpListenerBase)}:{nameof(ProcessConnection)}] new={connection.EndPoint}");
        }
        catch (System.Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(TcpListenerBase)}:{nameof(ProcessConnection)}] process-error={connection.EndPoint}", ex);

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

        // De-subscribe to prevent memory leaks
        args.Connection.OnCloseEvent -= this.HandleConnectionClose;
        args.Connection.OnCloseEvent -= _limiter.OnConnectionClosed;

        args.Connection.OnProcessEvent -= _protocol.ProcessMessage;
        args.Connection.OnPostProcessEvent -= _protocol.PostProcessMessage;

        args.Connection.Dispose();

        s_logger?.Trace($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleConnectionClose)}] close={args.Connection.EndPoint}");
    }

    [System.Diagnostics.DebuggerStepThrough]
    private IConnection InitializeConnection(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Net.Sockets.Socket socket,
        [System.Diagnostics.CodeAnalysis.NotNull] PooledAcceptContext context)
    {
        InitializeOptions(socket);

        // Trả context ngay tại đây, trước khi tạo connection
        // Context chỉ cần cho việc Accept, không cần sau đó
        s_pool.Return<PooledAcceptContext>(context);

        IConnection connection = new Connection(socket);

        connection.OnCloseEvent += this.HandleConnectionClose;
        connection.OnCloseEvent += _limiter.OnConnectionClosed;

        connection.OnProcessEvent += _protocol.ProcessMessage;
        connection.OnPostProcessEvent += _protocol.PostProcessMessage;

        if (s_config.EnableTimeout)
        {
            InstanceManager.Instance.GetOrCreateInstance<TimingWheel>()
                                    .Register(connection);
        }

        return connection;
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
                                    .Debug($"[NW.{nameof(TcpListenerBase)}:Internal] accept-error ex={ex.Message}");
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

                    if (socket.RemoteEndPoint is not System.Net.IPEndPoint remoteIp ||
                        !_limiter.IsConnectionAllowed(remoteIp))
                    {
                        SafeCloseSocket(socket);
                        throw new InternalErrorException();
                    }

                    // Create and process connection similar to async version
                    PooledAcceptContext context = ((PooledSocketAsyncEventArgs)args).Context!;
                    IConnection connection = this.InitializeConnection(socket, context);

                    // Process the connection
                    PooledListenerProcessContext ctx = s_pool.Get<PooledListenerProcessContext>();

                    ctx.Listener = this;
                    ctx.Connection = connection;

                    DISPATCH_CONNECTION(connection);

                    // Rebind a fresh context for the next accept on this args
                    PooledAcceptContext nextCtx = s_pool.Get<PooledAcceptContext>();

                    ((PooledSocketAsyncEventArgs)args).Context = nextCtx;
                    nextCtx.BindArgsForSync((PooledSocketAsyncEventArgs)args);
                }
                catch (System.ObjectDisposedException)
                {
                    s_logger?.Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] disposed-during-accept remote={socket.RemoteEndPoint}");

                    SafeCloseSocket(socket);
                    if (args is PooledSocketAsyncEventArgs pooled && pooled.Context != null)
                    {
                        s_pool.Return<PooledAcceptContext>(pooled.Context);

                        // Rebind a fresh context for next accepts on this args
                        PooledAcceptContext newCtx = s_pool.Get<PooledAcceptContext>();

                        pooled.Context = newCtx;
                        newCtx.BindArgsForSync(pooled);
                    }
                }
                catch (System.Exception ex)
                {
                    _metrics.RECORD_ERROR();
                    s_logger?.Error($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] accept-error port={_port}", ex);

                    SafeCloseSocket(socket);

                    s_pool.Return<PooledAcceptContext>(((PooledSocketAsyncEventArgs)args).Context!);

                    PooledAcceptContext newCtx = s_pool.Get<PooledAcceptContext>();

                    ((PooledSocketAsyncEventArgs)args).Context = newCtx;
                    newCtx.BindArgsForSync((PooledSocketAsyncEventArgs)args);
                }
            }
            else
            {
                s_logger?.Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(HandleAccept)}] accept-failed={args.SocketError}");

                if (args is PooledSocketAsyncEventArgs pooled)
                {
                    if (pooled.Context is not null)
                    {
                        s_pool.Return<PooledAcceptContext>(pooled.Context);
                    }

                    pooled.Context = s_pool.Get<PooledAcceptContext>();

                    pooled.Context.BindArgsForSync(pooled);
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
            s_pool.Return((PooledSocketAsyncEventArgs)args);
        }

        PooledAcceptContext context = s_pool.Get<PooledAcceptContext>();
        PooledSocketAsyncEventArgs newArgs = s_pool.Get<PooledSocketAsyncEventArgs>();

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
            catch (System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode is
                   System.Net.Sockets.SocketError.Interrupted or
                   System.Net.Sockets.SocketError.OperationAborted or
                   System.Net.Sockets.SocketError.ConnectionAborted)
            {
                // Expected during shutdown
                break;
            }
            catch (System.Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                s_logger?.Error($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptNext)}] accept-error port={_port}", ex);

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
        [System.Diagnostics.CodeAnalysis.NotNull] IWorkerContext ctx,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken cancellationToken)
    {
        System.TimeSpan heartbeatInterval = System.TimeSpan.FromSeconds(2);

        while (!cancellationToken.IsCancellationRequested)
        {
            ctx.Beat();

#if DEBUG
            s_logger?.Trace($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] waiting-accept port={_port}");
#endif

            IConnection connection;
            try
            {
                connection = await this.CreateConnectionAsync(cancellationToken)
                                       .ConfigureAwait(false);
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
#if DEBUG
                s_logger?.Trace($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] shutdown-requested port={_port}");
#endif
                break;
            }
            catch (InternalErrorException)
            {
                if (cancellationToken.IsCancellationRequested || State != ListenerState.RUNNING)
                {
                    break;
                }

                // Rate-limited / rejected connection — tiếp tục
                await System.Threading.Tasks.Task.Delay(10, System.Threading.CancellationToken.None)
                                                 .ConfigureAwait(false);
                continue;
            }
            catch (System.Net.Sockets.SocketException ex)
                when (IsIgnorableAcceptError(ex.SocketErrorCode, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested || State != ListenerState.RUNNING)
                {
                    break;
                }

                _metrics.RECORD_ERROR();
#if DEBUG
                s_logger?.Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] transient-socket-error={ex.SocketErrorCode} port={_port}");
#endif

                await System.Threading.Tasks.Task.Delay(50, System.Threading.CancellationToken.None)
                                                 .ConfigureAwait(false);
                continue;
            }
            catch (System.Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _metrics.RECORD_ERROR();
                s_logger?.Error($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] accept-error port={_port}", ex);

                await System.Threading.Tasks.Task.Delay(50, cancellationToken)
                                                 .ConfigureAwait(false);
                continue;
            }

#if DEBUG
            s_logger?.Trace($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] accepted remote={connection.EndPoint} port={_port}");
#endif

            PooledListenerProcessContext pctx = s_pool.Get<PooledListenerProcessContext>();
            pctx.Listener = this;
            pctx.Connection = connection;

            DISPATCH_CONNECTION(connection);
            ctx.Advance(1, note: "accepted");
        }

#if DEBUG
        s_logger?.Trace($"[NW.{nameof(TcpListenerBase)}:{nameof(AcceptConnectionsAsync)}] loop-exited port={_port}");
#endif
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

        System.Boolean contextReturned = false;
        PooledAcceptContext context = s_pool.Get<PooledAcceptContext>();

        try
        {
            System.Net.Sockets.Socket socket;

            if (_listener == null)
            {
                throw new System.InvalidOperationException("Socket is not initialized.");
            }

            // Wait async accept:
            socket = await context.BeginAcceptAsync(_listener, cancellationToken)
                                  .ConfigureAwait(false);

            if (!_limiter.IsConnectionAllowed(socket.RemoteEndPoint))
            {
                SafeCloseSocket(socket);

                // Trả context ở đây vì InitializeConnection sẽ không được gọi
                contextReturned = true;
                s_pool.Return<PooledAcceptContext>(context);

                _metrics.RECORD_REJECTED();
                throw new InternalErrorException();
            }

            return this.InitializeConnection(socket, context);
        }
        catch (System.OperationCanceledException)
        {
            if (!contextReturned)
            {
                s_pool.Return<PooledAcceptContext>(context);
            }

            throw;
        }
        catch (InternalErrorException)
        {
            throw;
        }
        catch (System.Exception ex)
        {
            if (!contextReturned)
            {
                s_pool.Return<PooledAcceptContext>(context);
            }

            throw new InternalErrorException("Accept failed", ex);
        }
    }
}