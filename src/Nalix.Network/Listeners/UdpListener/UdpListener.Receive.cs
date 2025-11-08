// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Logging;
using Nalix.Common.Packets;
using Nalix.Framework.Identity;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Framework.Tasks.Options;
using Nalix.Network.Connection;
using Nalix.Network.Internal.Net;

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private async System.Threading.Tasks.Task ReceiveDatagramsAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        System.ArgumentNullException.ThrowIfNull(_udpClient);
        System.ObjectDisposedException.ThrowIf(_isDisposed, this);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                System.Net.Sockets.UdpReceiveResult result = await _udpClient.ReceiveAsync(cancellationToken)
                                                                             .ConfigureAwait(false);

                System.Int32 next = System.Threading.Interlocked.Increment(ref _procSeq);
                System.Int32 idx = next & System.Int32.MaxValue;

                _ = InstanceManager.Instance.GetExistingInstance<TaskManager>()?.StartWorker(
                    name: NetTaskCatalog.UdpProcessWorker(_port, idx),
                    group: NetTaskCatalog.UdpProcessGroup(_port),
                    work: (_, __) => { ProcessDatagram(result); return new System.Threading.Tasks.ValueTask(); },
                    options: new WorkerOptions
                    {
                        Tag = nameof(NetTaskCatalog.Segments.Udp),
                        GroupConcurrencyLimit = Config.MaxGroupConcurrency,
                        TryAcquireSlotImmediately = true,
                        CancellationToken = cancellationToken
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
                                        .Error($"[{nameof(UdpListenerBase)}:{nameof(ReceiveDatagramsAsync)}] " +
                                               $"recv-error port={_port}", ex);

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
                                    .Debug($"[{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] " +
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

        IIdentifier identifier = Identifier.FromBytes(result.Buffer[^Identifier.Size..]);

        if (InstanceManager.Instance.GetExistingInstance<ConnectionHub>()!
                                    .GetConnection(identifier) is not Connection.Connection connection)
        {
            System.Threading.Interlocked.Increment(ref _dropUnknown);
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] unknown-packet from={result.RemoteEndPoint}");
            return;
        }

        if (!this.IsAuthenticated(connection, result))
        {
            System.Threading.Interlocked.Increment(ref _dropUnauth);
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] unauth from={result.RemoteEndPoint}");
            return;
        }

        System.Threading.Interlocked.Increment(ref _rxPackets);
        System.Threading.Interlocked.Add(ref _rxBytes, result.Buffer.Length);

        connection.InjectIncoming(result.Buffer[..^Identifier.Size]);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Meta($"[{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] inject id={connection.ID} size={result.Buffer.Length}");
    }
}

