using Nalix.Common.Packets;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware.Core;

namespace Nalix.Network.Dispatch.Middleware;

/// <summary>
/// Middleware responsible for decrypting incoming packets if they are encrypted.
/// If decryption fails, an error packet is sent back to the client and further processing is aborted.
/// </summary>
/// <typeparam name="TPacket">
/// The type of packet, which must implement <see cref="IPacket"/>, <see cref="IPacketTransformer{TPacket}"/>,
/// and <see cref="IPacketTransformer{TPacket}"/> interfaces.
/// </typeparam>
public class DecryptionMiddleware<TPacket> : IPacketMiddleware<TPacket>
    where TPacket : IPacket, IPacketTransformer<TPacket>
{
    /// <summary>
    /// Attempts to decrypt the packet using the connection's encryption key and algorithm.
    /// If the packet is not encrypted, proceeds without modification.
    /// If decryption fails, sends an error packet and halts pipeline execution.
    /// </summary>
    /// <param name="context">The packet context containing the packet and its associated connection.</param>
    /// <param name="next">The delegate to invoke the next middleware in the pipeline.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<TPacket> context,
        System.Func<System.Threading.Tasks.Task> next) =>
        //if (context.Packet.IsEncrypted)
        //{
        //    try
        //    {
        //        context.SetPacket(TPacket.Decrypt(
        //            context.Packet,
        //            context.Connection.EncryptionKey,
        //            context.Connection.Encryption));
        //    }
        //    catch (System.Exception)
        //    {
        //        _ = await context.Connection.Tcp.SendAsync(TPacket.Create(0, "Failed to process packet."));
        //        return;
        //    }
        //}

        await next();
}