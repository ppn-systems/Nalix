using Nalix.Common.Package;

namespace Nalix.Network.Dispatch.Middleware.Inbound;

/// <summary>
/// Middleware responsible for decrypting incoming packets if they are encrypted.
/// If decryption fails, an error packet is sent back to the client and further processing is aborted.
/// </summary>
/// <typeparam name="TPacket">
/// The type of packet, which must implement <see cref="IPacket"/>, <see cref="IPacketEncryptor{TPacket}"/>,
/// and <see cref="IPacketFactory{TPacket}"/> interfaces.
/// </typeparam>
public class DecryptionMiddleware<TPacket> : IPacketMiddleware<TPacket>
    where TPacket : IPacket, IPacketEncryptor<TPacket>, IPacketFactory<TPacket>
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
        System.Func<System.Threading.Tasks.Task> next)
    {
        if (context.Packet.IsEncrypted)
        {
            try
            {
                context.Packet = TPacket.Decrypt(
                    context.Packet,
                    context.Connection.EncryptionKey,
                    context.Connection.Encryption);
            }
            catch (System.Exception)
            {
                await context.Connection.Tcp.SendAsync(TPacket.Create(0, "Packet decoding failed!"));
                return;
            }
        }

        await next();
    }
}