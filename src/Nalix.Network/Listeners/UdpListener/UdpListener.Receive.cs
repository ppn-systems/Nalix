// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets;
using Nalix.Common.Security.Abstractions;
using Nalix.Framework.Identity;
using Nalix.Network.Connection;
using Nalix.Shared.Injection;

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    private async System.Threading.Tasks.Task ReceiveDatagramsAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        System.ArgumentNullException.ThrowIfNull(this._udpClient);
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                System.Net.Sockets.UdpReceiveResult result = await this._udpClient
                    .ReceiveAsync(cancellationToken)
                    .ConfigureAwait(false);

                _ = System.Threading.ThreadPool.UnsafeQueueUserWorkItem(
                    static state =>
                    {
                        CallbackState s = (CallbackState)state!;
                        s.Listener.ProcessDatagram(s.Result);
                    },
                    new CallbackState { Listener = this, Result = result });
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (System.Exception ex) when (!cancellationToken.IsCancellationRequested)
            {

                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(UdpListenerBase)}] Receive error on {Config.Port}: {ex.Message}");

                await System.Threading.Tasks.Task.Delay(50, cancellationToken)
                                                 .ConfigureAwait(false);
            }
        }
    }

    private void ProcessDatagram(System.Net.Sockets.UdpReceiveResult result)
    {
        if (result.Buffer.Length < PacketConstants.HeaderSize + Identifier.Size)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(UdpListenerBase)}] " +
                                           $"Packet too short from {result.RemoteEndPoint}: {result.Buffer.Length} bytes");
            return;
        }

        IIdentifier identifier = Identifier.Deserialize(result.Buffer[^Identifier.Size..]);

        if (InstanceManager.Instance.GetExistingInstance<ConnectionHub>() is null)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(UdpListenerBase)}] " +
                                           $"ConnectionHub is not registered in InstanceManager");
            return;
        }
        if (InstanceManager.Instance.GetExistingInstance<ConnectionHub>()!
                                    .GetConnection(identifier) is not Connection.Connection connection)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(UdpListenerBase)}] Unidentified packet from {result.RemoteEndPoint}");
            return;
        }

        if (!this.IsAuthenticated(connection, result))
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(UdpListenerBase)}] Unauthenticated packet from {result.RemoteEndPoint}");
            return;
        }

        connection.InjectIncoming(result.Buffer[..^7]);
    }

    private struct CallbackState
    {
        public required UdpListenerBase Listener;
        public required System.Net.Sockets.UdpReceiveResult Result;
    }
}

