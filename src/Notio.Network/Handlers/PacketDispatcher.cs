using Notio.Common.Connection;
using Notio.Common.Package;

namespace Notio.Network.Handlers;

/// <summary>
/// Ultra-high performance packet dispatcher with advanced dependency injection (DI) integration and async support.
/// This implementation uses reflection to map packet command IDs to controller methods.
/// </summary>
/// <remarks>
/// The <see cref="PacketDispatcher"/> processes incoming packets and invokes corresponding handlers
/// based on the registered command IDs. It logs errors and warnings when handling failures or unregistered commands.
/// </remarks>
/// <param name="options">
/// A delegate used to configure <see cref="PacketDispatcherOptions"/> before processing packets.
/// </param>
public class PacketDispatcher(System.Action<PacketDispatcherOptions> options)
    : PacketDispatcherBase(options), IPacketDispatcher
{
    /// <inheritdoc />
    public void HandlePacket(byte[]? packet, IConnection connection)
    {
        if (packet == null)
        {
            Logger?.Error($"No packet data provided from Ip:{connection.RemoteEndPoint}.");
            return;
        }

        if (Options.DeserializationMethod == null)
        {
            Logger?.Error("No deserialization method specified.");
            return;
        }

        Logger?.Debug("Deserializing packet...");
        IPacket parsedPacket = Options.DeserializationMethod(packet);
        Logger?.Debug("Packet deserialized successfully.");

        this.HandlePacket(parsedPacket, connection).Wait();
    }

    /// <inheritdoc />
    public void HandlePacket(System.ReadOnlyMemory<byte>? packet, IConnection connection)
    {
        if (packet == null)
        {
            Logger?.Error($"No packet data provided from Ip:{connection.RemoteEndPoint}.");
            return;
        }

        if (Options.DeserializationMethod == null)
        {
            Logger?.Error("No deserialization method specified.");
            return;
        }

        Logger?.Debug("Deserializing packet...");
        IPacket parsedPacket = Options.DeserializationMethod(packet.Value);
        Logger?.Debug("Packet deserialized successfully.");

        this.HandlePacket(parsedPacket, connection).Wait();
    }

    /// <inheritdoc />
    public async System.Threading.Tasks.Task HandlePacket(IPacket? packet, IConnection connection)
    {
        if (packet == null)
        {
            Logger?.Error($"No packet data provided from Ip:{connection.RemoteEndPoint}.");
            return;
        }

        ushort commandId = packet.Command;
        Logger?.Debug($"Processing packet with CommandId: {commandId}");

        if (Options.PacketHandlers.TryGetValue(commandId, out var handlerAction))
        {
            Logger?.Debug($"Invoking handler for CommandId: {commandId}");

            try
            {
                await handlerAction(packet, connection).ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                Logger?.Error($"Error handling packet with CommandId {commandId}: {ex.Message}", ex);
            }
        }
        else
        {
            Logger?.Warn($"No handler found for CommandId {commandId}");
        }
    }
}
