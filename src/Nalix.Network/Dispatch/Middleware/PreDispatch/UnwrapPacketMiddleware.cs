using Nalix.Common.Packets.Enums;
using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware.Core;
using Nalix.Shared.Extensions;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Transport;

namespace Nalix.Network.Dispatch.Middleware.Pre;

/// <summary>
/// Middleware that unwraps (decrypts and/or decompresses) packets before further processing.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type, which must implement <see cref="IPacket"/> and <see cref="IPacketTransformer{TPacket}"/>.
/// </typeparam>
[PacketMiddleware(MiddlewareStage.PreDispatch, order: 3, name: "Unwrap")]
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
        catch (System.Exception)
        {
            LiteralPacket text = ObjectPoolManager.Instance.Get<LiteralPacket>();
            try
            {
                text.Initialize($"Packet transform failed.");
                _ = await context.Connection.Tcp.SendAsync(text.Serialize());

                return;
            }
            finally
            {
                ObjectPoolManager.Instance.Return<LiteralPacket>(text);
            }
        }

        await next();
    }
}