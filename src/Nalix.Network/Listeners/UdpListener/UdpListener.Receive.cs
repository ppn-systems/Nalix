// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets;
using Nalix.Framework.Identity;
using Nalix.Framework.Injection;
using Nalix.Network.Connection;

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
                                        .Error($"[{nameof(UdpListenerBase)}] recv-error port={_port}", ex);

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
                                           $"short-packet len={result.Buffer.Length} from={result.RemoteEndPoint}");
            return;
        }

        if (InstanceManager.Instance.GetExistingInstance<ConnectionHub>() is null)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(UdpListenerBase)}] [{nameof(ConnectionHub)}] null");
            return;
        }

        IIdentifier identifier = Identifier.Deserialize(result.Buffer[^Identifier.Size..]);

        if (InstanceManager.Instance.GetExistingInstance<ConnectionHub>()!
                                    .GetConnection(identifier) is not Connection.Connection connection)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(UdpListenerBase)}] unknown-packet from={result.RemoteEndPoint}");
            return;
        }

        if (!this.IsAuthenticated(connection, result))
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(UdpListenerBase)}] unauth from={result.RemoteEndPoint}");
            return;
        }

        connection.InjectIncoming(result.Buffer[..^Identifier.Size]);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Meta($"[{nameof(UdpListenerBase)}] inject id={connection.ID} size={result.Buffer.Length}");
    }

    private struct CallbackState
    {
        public required UdpListenerBase Listener;
        public required System.Net.Sockets.UdpReceiveResult Result;
    }
}

