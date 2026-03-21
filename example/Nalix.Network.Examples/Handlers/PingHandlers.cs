// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.Network.Connections;
using Nalix.Network.Routing;
using Nalix.Shared.Frames.Controls;
using Nalix.Shared.Memory.Objects;

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
    [PacketEncryption(true)]
    [PacketPermission(PermissionLevel.GUEST)]
    public static async System.Threading.Tasks.Task Ping(PacketContext<IPacket> context)
    {
        UInt32 fallbackSeq = context.Packet.SequenceId;
        System.Console.WriteLine("Received PING from " + context.Connection.RemoteEndPoint);
        // Chỉ nhận gói Control có type = PING
        if (context.Packet is not Handshake ping)
        {
            System.Console.WriteLine($"[APP.{nameof(PingHandlers)}] Received non-PING packet from {context.Connection.RemoteEndPoint}");
            await context.Connection.SendAsync(
                ControlType.ERROR,
                ProtocolReason.MALFORMED_PACKET,
                ProtocolAdvice.DO_NOT_RETRY).ConfigureAwait(false);

            return;
        }
        try
        {
            // Gửi Control PONG về client
            await context.Sender.SendAsync(ping).ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            s_logger?.Error($"[APP.{nameof(PingHandlers)}] failed ep={context.Connection.RemoteEndPoint} ex={ex.Message}");

            await context.Connection.SendAsync(
                ControlType.ERROR,
                ProtocolReason.INTERNAL_ERROR,
                ProtocolAdvice.BACKOFF_RETRY,
                sequenceId: ping.SequenceId,
                flags: ControlFlags.IS_TRANSIENT).ConfigureAwait(false);
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