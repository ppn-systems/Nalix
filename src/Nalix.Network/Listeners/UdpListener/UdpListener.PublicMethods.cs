// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Logging.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Framework.Tasks.Options;
using Nalix.Framework.Time;
using Nalix.Network.Abstractions;

namespace Nalix.Network.Listeners.Udp;

/// <summary>
/// Provides a base implementation for a UDP network listener, supporting asynchronous listening,
/// protocol processing, and time synchronization. Inherit from this class to implement custom UDP listeners.
/// </summary>
[System.Diagnostics.DebuggerDisplay("Port={Config?.Port}, Running={_isRunning}")]
public abstract partial class UdpListenerBase : IListener
{
    /// <summary>
    /// Starts listening for incoming UDP datagrams and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="System.Threading.CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="System.Threading.CancellationToken"/> to cancel the listening process.</param>
    [System.Diagnostics.DebuggerStepThrough]
    public void Activate(
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        if (this._isRunning)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(UdpListenerBase)}] already-running");
            return;
        }

        if (this._udpClient == null)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(UdpListenerBase)}] init port={_port}");
            this.Initialize();
        }

        try
        {
            this._isRunning = true;
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(UdpListenerBase)}] start protocol={_protocol} port={_port}");

            this._cts?.Dispose();
            this._cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this._cancellationToken = this._cts.Token;

            _lock.Wait(_cancellationToken);

            try
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[{nameof(UdpListenerBase)}] listening port={_port}");

                System.Threading.Tasks.Task receiveTask = this.ReceiveDatagramsAsync(this._cancellationToken);
                _ = receiveTask.ConfigureAwait(false);

                _ = InstanceManager.Instance.GetExistingInstance<TaskManager>()?.StartWorker(
                   name: $"udp.proc.{_port}",
                   group: $"net/udp/{_port}",
                   work: async (_, ct) => await ReceiveDatagramsAsync(ct),
                   options: new WorkerOptions
                   {
                       Tag = "udp",
                       CancellationToken = _cancellationToken
                   });
            }
            finally
            {
                _ = _lock.Release();
            }
        }
        catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(UdpListenerBase)}] cancel port={_port}");
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Fatal($"[{nameof(UdpListenerBase)}] bind-fail port={_port}", ex);
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Fatal($"[{nameof(UdpListenerBase)}] critical port={_port}", ex);
        }
        finally
        {
            if (this._isRunning)
            {
                try
                {
                    this._isRunning = false;
                    this._cts?.Cancel();

                    if (this._udpClient != null)
                    {
                        this._udpClient.Close();
                        _ = System.Threading.Tasks.Task.Delay(200, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (System.Exception ex)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[{nameof(UdpListenerBase)}] shutdown-error", ex);
                }
                finally
                {
                    this._cts?.Dispose();
                    this._cts = null;
                }
            }
        }
    }

    /// <summary>
    /// Stops the listener from receiving further UDP datagrams.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    public void Deactivate(
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        this._cts?.Cancel();

        try
        {
            if (this._isRunning)
            {
                this._udpClient?.Close();
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[{nameof(UdpListenerBase)}] stopped port={_port}");
            }
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Error($"[{nameof(UdpListenerBase)}] stop-error", ex);
        }
        finally
        {
            this._isRunning = false;
            this._cts?.Dispose();
            this._cts = null;
        }
    }

    /// <summary>
    /// Updates the listener with the current server time, provided as a Unix timestamp.
    /// </summary>
    /// <param name="milliseconds">The current server time in milliseconds since the Unix epoch (January 1, 2020, 00:00:00 UTC), as provided by <see cref="Clock.UnixMillisecondsNow"/>.</param>
    [System.Diagnostics.DebuggerStepThrough]
    public virtual void SynchronizeTime(System.Int64 milliseconds)
    {
    }

    /// <summary>
    /// Determines whether the incoming packet is authenticated.
    /// Default returns true (i.e., trusted). Override in derived class.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    protected abstract System.Boolean IsAuthenticated(
        IConnection connection, in System.Net.Sockets.UdpReceiveResult result);
}