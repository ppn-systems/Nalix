// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Networking.Abstractions;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Packets.Attributes;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security.Enums;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.Network.Connections;
using Nalix.Network.Routing;
using Nalix.Shared.Frames.Controls;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Examples.Handlers;

/// <summary>
/// Xử lý ControlType.PING từ client: xác thực và trả về ControlType.PONG.
/// </summary>
[PacketController]
public sealed class PingHandlers
{
    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    [PacketOpcode(0)]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.GUEST)]
    public static async System.Threading.Tasks.Task Ping(
        IPacket p,
        IConnection connection)
    {
        // Chỉ nhận gói Control có type = PING
        if (p is not Control ping || ping.Type != ControlType.PING)
        {
            System.UInt32 fallbackSeq = p is IPacketSequenced ps ? ps.SequenceId : 0;
            await connection.SendAsync(
                ControlType.ERROR,
                ProtocolReason.MALFORMED_PACKET,
                ProtocolAdvice.DO_NOT_RETRY, fallbackSeq).ConfigureAwait(false);

            return;
        }

        // Tạo PONG response frame (echo lại SequenceId, MonoTicks mới, timestamp mới)
        Control pong = s_pool.Get<Control>();

        try
        {
            pong.Initialize(
                opCode: ping.OpCode,      // Echo lại OpCode giống client gửi lên
                type: ControlType.PONG,
                sequenceId: ping.SequenceId,
                reasonCode: ProtocolReason.NONE,    // Không lỗi
                transport: ping.Protocol);

            pong.MonoTicks = ping.MonoTicks; // Option: echo lại MonoTicks client gửi lên (để RTT tốt nhất)
            pong.Timestamp = Clock.UnixMillisecondsNow();

            // Gửi Control PONG về client
            await connection.TCP.SendAsync(pong.Serialize()).ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            s_logger?.Error($"[APP.{nameof(PingHandlers)}] failed ep={connection.RemoteEndPoint} ex={ex.Message}");

            await connection.SendAsync(
                ControlType.ERROR,
                ProtocolReason.INTERNAL_ERROR,
                ProtocolAdvice.BACKOFF_RETRY,
                sequenceId: ping.SequenceId,
                flags: ControlFlags.IS_TRANSIENT).ConfigureAwait(false);
        }
        finally
        {
            s_pool.Return<Control>(pong);
        }
    }

    [PacketOpcode(1)]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.GUEST)]
    public static async Task Pong(PacketContext<IPacket> p) // or PacketContext<Control>
    {
        // Chỉ nhận gói Control có type = PING
        if (p.Packet is not Control pong || pong.Type != ControlType.PING)
        {
            System.UInt32 fallbackSeq = p.Packet is IPacketSequenced ps ? ps.SequenceId : 0;

            // Not auto enc, compress
            await p.Connection.SendAsync(
                ControlType.ERROR,
                ProtocolReason.MALFORMED_PACKET,
                ProtocolAdvice.DO_NOT_RETRY, fallbackSeq).ConfigureAwait(false);

            return;
        }

        // Tạo PONG response frame (echo lại SequenceId, MonoTicks mới, timestamp mới)
        Control ping = s_pool.Get<Control>();

        try
        {
            ping.Initialize(
                opCode: pong.OpCode,      // Echo lại OpCode giống client gửi lên
                type: ControlType.PONG,
                sequenceId: pong.SequenceId,
                reasonCode: ProtocolReason.NONE,    // Không lỗi
                transport: pong.Protocol);

            ping.MonoTicks = pong.MonoTicks; // Option: echo lại MonoTicks client gửi lên (để RTT tốt nhất)
            ping.Timestamp = Clock.UnixMillisecondsNow();

            // Gửi Control PONG về client
            // Auto encrypt, compress theo thiết lập attribute [PacketEncryption], [PacketCompression] trên handler
            await p.Sender.SendAsync(ping).ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            s_logger?.Error($"[APP.{nameof(PingHandlers)}] failed ep={p.Connection.RemoteEndPoint} ex={ex.Message}");

            await p.Connection.SendAsync(
                ControlType.ERROR,
                ProtocolReason.INTERNAL_ERROR,
                ProtocolAdvice.BACKOFF_RETRY,
                sequenceId: pong.SequenceId,
                flags: ControlFlags.IS_TRANSIENT).ConfigureAwait(false);
        }
        finally
        {
            s_pool.Return<Control>(pong);
        }
    }
}