using Notio.Common.Connection;
using Notio.Common.Package;
using System;
using System.Threading.Tasks;

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
public class PacketDispatcher(Action<PacketDispatcherOptions> options)
    : PacketDispatcherBase(options), IPacketDispatcher
{
    /// <summary>
    /// Processes an incoming byte array packet by deserializing it and invoking the associated handler method.
    /// </summary>
    /// <param name="packet">The byte array representing the received packet to be processed.</param>
    /// <param name="connection">The connection through which the packet was received.</param>
    /// <remarks>
    /// If a handler is registered for the packet's command ID, it will be invoked.
    /// If no handler is found, a warning is logged. Errors occurring during execution are also logged.
    /// </remarks>
    public void HandlePacket(byte[]? packet, IConnection connection)
    {
        if (packet == null)
        {
            Options.Logger?.Error($"No packet data provided from Ip:{connection.RemoteEndPoint}.");
            return;
        }

        if (Options.DeserializationMethod == null)
        {
            Options.Logger?.Error("No deserialization method specified.");
            return;
        }

        IPacket parsedPacket = Options.DeserializationMethod(packet);

        this.HandlePacket(parsedPacket, connection);
    }

    public void HandlePacket(ReadOnlyMemory<byte>? packet, IConnection connection)
    {
        if (packet == null)
        {
            Options.Logger?.Error($"No packet data provided from Ip:{connection.RemoteEndPoint}.");
            return;
        }

        if (Options.DeserializationMethod == null)
        {
            Options.Logger?.Error("No deserialization method specified.");
            return;
        }

        IPacket parsedPacket = Options.DeserializationMethod(packet.Value);

        this.HandlePacket(parsedPacket, connection);
    }

    /// <summary>
    /// Processes an incoming packet by looking up the command ID and invoking the associated handler method.
    /// </summary>
    /// <param name="packet">The received packet to be processed.</param>
    /// <param name="connection">The connection through which the packet was received.</param>
    /// <remarks>
    /// If a handler is registered for the packet's command ID, it will be invoked.
    /// If no handler is found, a warning is logged. Errors occurring during execution are also logged.
    /// </remarks>
    public void HandlePacket(IPacket? packet, IConnection connection)
    {
        if (packet == null)
        {
            Options.Logger?.Error($"No packet data provided from Ip:{connection.RemoteEndPoint}.");
            return;
        }

        ushort commandId = packet.Command;

        if (Options.PacketHandlers.TryGetValue(commandId, out Func<IPacket, IConnection, Task>? handlerAction))
        {
            try
            {
                handlerAction.Invoke(packet, connection);
            }
            catch (Exception ex)
            {
                Options.Logger?.Error($"Error handling packet with CommandId {commandId}: {ex.Message}");
            }

            return;
        }

        Options.Logger?.Warn($"No handler found for CommandId {commandId}");
    }
}
