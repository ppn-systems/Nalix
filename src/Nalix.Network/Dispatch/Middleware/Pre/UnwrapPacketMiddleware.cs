using Nalix.Common.Packets;
using Nalix.Common.Packets.Enums;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware.Core;
using Nalix.Shared.Extensions;

namespace Nalix.Network.Dispatch.Middleware.Pre;

/// <summary>
/// Middleware that unwraps (decrypts and/or decompresses) packets before further processing.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type, which must implement <see cref="IPacket"/> and <see cref="IPacketTransformer{TPacket}"/>.
/// </typeparam>
[PacketMiddleware(MiddlewareStage.Pre, order: 3, name: "Unwrap")]
public class UnwrapPacketMiddleware<TPacket> : IPacketMiddleware<TPacket>
    where TPacket : IPacket, IPacketTransformer<TPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<TPacket> context,
        System.Func<System.Threading.Tasks.Task> next)
    {
        try
        {
            TPacket current = context.Packet;

            if (context.Packet.Flags.HasFlag<PacketFlags>(PacketFlags.Encrypted))
            {
                current = TPacket.Decrypt(current, context.Connection.EncryptionKey, context.Connection.Encryption);
            }

            if (context.Packet.Flags.HasFlag<PacketFlags>(PacketFlags.Compressed))
            {
                current = TPacket.Decompress(current);
            }

            if (!ReferenceEquals(current, context.Packet))
            {
                context.SetPacket(current);
            }
        }
        catch (System.Exception ex)
        {
            _ = await context.Connection.Tcp.SendAsync(
                TPacket.Create(0, "Packet transform failed: " + ex.Message));

            return;
        }

        await next();
    }
}