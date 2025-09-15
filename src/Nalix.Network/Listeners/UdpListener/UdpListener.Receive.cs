// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets;
using Nalix.Framework.Identity;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Framework.Tasks.Options;
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

                _ = InstanceManager.Instance.GetExistingInstance<TaskManager>()?.StartWorker(
                    name: $"udp.proc.{_port}",
                    group: $"udp.port.{_port}",
                    work: (_, __) => { ProcessDatagram(result); return new System.Threading.Tasks.ValueTask(); },
                    options: new WorkerOptions
                    {
                        Tag = "udp",
                        MaxGroupConcurrency = Config.MaxGroupConcurrency,
                        TryAcquireGroupSlotImmediately = true
                    });
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (System.Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                System.Threading.Interlocked.Increment(ref _recvErrors);
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
            System.Threading.Interlocked.Increment(ref _dropShort);
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(UdpListenerBase)}] [{nameof(ConnectionHub)}] null");
            return;
        }

        IIdentifier identifier = Identifier.FromByteArray(result.Buffer[^Identifier.Size..]);

        if (InstanceManager.Instance.GetExistingInstance<ConnectionHub>()!
                                    .GetConnection(identifier) is not Connection.Connection connection)
        {
            System.Threading.Interlocked.Increment(ref _dropUnknown);
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(UdpListenerBase)}] unknown-packet from={result.RemoteEndPoint}");
            return;
        }

        if (!this.IsAuthenticated(connection, result))
        {
            System.Threading.Interlocked.Increment(ref _dropUnauth);
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(UdpListenerBase)}] unauth from={result.RemoteEndPoint}");
            return;
        }

        System.Threading.Interlocked.Increment(ref _rxPackets);
        System.Threading.Interlocked.Add(ref _rxBytes, result.Buffer.Length);

        connection.InjectIncoming(result.Buffer[..^Identifier.Size]);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Meta($"[{nameof(UdpListenerBase)}] inject id={connection.ID} size={result.Buffer.Length}");
    }
}

