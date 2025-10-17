// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Identity;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;
using Nalix.Framework.Time;
using Nalix.Network.Connections;
using Nalix.Network.Internal;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Security.Hashing;

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    private const long MaxReplayWindowMs = 30_000;
    private const int TimestampSize = sizeof(long);
    private const int AuthenticationTagSize = Poly1305.TagSize;
    private const int AuthenticationMetadataSize = Snowflake.Size + TimestampSize + AuthenticationTagSize;

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private async System.Threading.Tasks.Task ReceiveDatagramsAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        System.ArgumentNullException.ThrowIfNull(_udpClient);
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _isDisposed) != 0, this);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                System.Net.Sockets.UdpReceiveResult result = await _udpClient.ReceiveAsync(cancellationToken)
                                                                             .ConfigureAwait(false);

                int next = System.Threading.Interlocked.Increment(ref _procSeq);
                int idx = next & int.MaxValue;

                _ = InstanceManager.Instance.GetExistingInstance<TaskManager>()?.ScheduleWorker(
                    name: $"{NetTaskNames.Udp}.{TaskNaming.Tags.Accept}",
                    group: $"{NetTaskNames.Net}/{NetTaskNames.Udp}/{_port}",
                    work: (_, __) =>
                    {
                        ProcessDatagram(result); return new System.Threading.Tasks.ValueTask();
                    },
                    options: new WorkerOptions
                    {
                        Tag = NetTaskNames.Udp,
                        IdType = SnowflakeType.System,
                        TryAcquireSlotImmediately = true,
                        CancellationToken = cancellationToken,
                        GroupConcurrencyLimit = Config.MaxGroupConcurrency
                    });
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (System.Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _ = System.Threading.Interlocked.Increment(ref _recvErrors);
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[NW.{nameof(UdpListenerBase)}:{nameof(ReceiveDatagramsAsync)}] recv-error port={_port}", ex);

                await System.Threading.Tasks.Task.Delay(50, cancellationToken)
                                                 .ConfigureAwait(false);
            }
        }
    }

    private void ProcessDatagram(System.Net.Sockets.UdpReceiveResult result)
    {
        if (result.Buffer.Length < PacketConstants.HeaderSize + AuthenticationMetadataSize)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] short-packet len={result.Buffer.Length} from={result.RemoteEndPoint}");
            return;
        }

        byte[] buffer = result.Buffer;
        int payloadLength = buffer.Length - AuthenticationMetadataSize;

        System.ReadOnlySpan<byte> idBytes = System.MemoryExtensions.AsSpan(buffer, payloadLength, Snowflake.Size);
        System.ReadOnlySpan<byte> timestampBytes = System.MemoryExtensions.AsSpan(buffer, payloadLength + Snowflake.Size, TimestampSize);
        System.ReadOnlySpan<byte> tagBytes = System.MemoryExtensions.AsSpan(buffer, payloadLength + Snowflake.Size + TimestampSize, AuthenticationTagSize);

        long timestamp = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(timestampBytes);
        ISnowflake identifier = Snowflake.FromBytes(idBytes);

        ConnectionHub hub = InstanceManager.Instance.GetExistingInstance<ConnectionHub>();

        if (hub is null)
        {
            _ = System.Threading.Interlocked.Increment(ref _dropShort);
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] [{nameof(ConnectionHub)}] null");
            return;
        }

        if (hub.GetConnection(identifier) is not Connection connection)
        {
            _ = System.Threading.Interlocked.Increment(ref _dropUnknown);
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] unknown-packet from={result.RemoteEndPoint}");
            return;
        }

        BufferLease lease = BufferLease.CopyFrom(System.MemoryExtensions.AsSpan(buffer)[..payloadLength]);

        if (!ValidateAuthenticationToken(connection, result.RemoteEndPoint, lease.Span, idBytes, timestamp, tagBytes))
        {
            _ = System.Threading.Interlocked.Increment(ref _dropUnauth);
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] auth-fail id={connection.ID} from={result.RemoteEndPoint}");
            return;
        }

        if (!IsAuthenticated(connection, result))
        {
            _ = System.Threading.Interlocked.Increment(ref _dropUnauth);
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] unauth from={result.RemoteEndPoint}");
            return;
        }

        _ = System.Threading.Interlocked.Increment(ref _rxPackets);
        _ = System.Threading.Interlocked.Add(ref _rxBytes, result.Buffer.Length);

        connection.InjectIncoming(lease);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessDatagram)}] inject id={connection.ID} size={lease.Length}");
    }

    private static bool ValidateAuthenticationToken(
        Connection connection,
        System.Net.EndPoint remoteEndPoint,
        System.ReadOnlySpan<byte> payload,
        System.ReadOnlySpan<byte> identifierBytes,
        long timestamp,
        System.ReadOnlySpan<byte> expectedTag)
    {
        if (connection.Secret is null || connection.Secret.Length < Poly1305.KeySize)
        {
            return false;
        }

        long now = Clock.UnixMillisecondsNow();
        if (System.Math.Abs(now - timestamp) > MaxReplayWindowMs)
        {
            return false;
        }

        System.Span<byte> remoteMeta = stackalloc byte[1 + 16 + sizeof(ushort)];
        int remoteLength = EncodeRemoteEndpoint(remoteEndPoint, remoteMeta);
        if (remoteLength == 0)
        {
            return false;
        }

        bool isValid;

        Poly1305 poly = new(System.MemoryExtensions.AsSpan(connection.Secret, 0, Poly1305.KeySize));

        try
        {
            System.Span<byte> computedTag = new byte[AuthenticationTagSize];
            poly.Update(payload);
            poly.Update(identifierBytes);

            System.Span<byte> timestampBytes = stackalloc byte[TimestampSize];
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(timestampBytes, timestamp);

            poly.Update(timestampBytes);
            poly.Update(remoteMeta[..remoteLength]);
            poly.FinalizeTag(computedTag);

            isValid = FixedTimeEquals(expectedTag, computedTag);
        }
        finally
        {
            poly.Clear();
        }

        return isValid;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int EncodeRemoteEndpoint(System.Net.EndPoint remoteEndPoint, System.Span<byte> destination)
    {
        if (remoteEndPoint is not System.Net.IPEndPoint endpoint)
        {
            return 0;
        }

        byte[] addressBytes = endpoint.Address.GetAddressBytes();
        if (addressBytes.Length > 16 || destination.Length < 1 + addressBytes.Length + sizeof(ushort))
        {
            return 0;
        }

        destination[0] = (byte)addressBytes.Length;
        System.MemoryExtensions.AsSpan(addressBytes)
                               .CopyTo(destination
                               .Slice(1, addressBytes.Length));

        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(destination[(1 + addressBytes.Length)..], (ushort)endpoint.Port);

        return 1 + addressBytes.Length + sizeof(ushort);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool FixedTimeEquals(System.ReadOnlySpan<byte> left, System.ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        int result = 0;

        for (int i = 0; i < left.Length; i++)
        {
            result |= left[i] ^ right[i];
        }

        return result == 0;
    }
}
