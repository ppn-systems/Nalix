using Notio.Common.Connection;
using Notio.Common.Package;
using System;

namespace Notio.Network.Handlers;

/// <summary>
/// Ultra-high performance packet router with advanced DI integration and async support.
/// This implementation uses reflection to map packet command IDs to controller methods.
/// </summary>
public class PacketDispatcher(Action<PacketDispatcherOptions> options) : PacketDispatcherBase(options), IPacketDispatcher
{
    /// <summary>
    /// Processes an incoming packet based on the command ID and invokes the corresponding handler method.
    /// </summary>
    public void HandlePacket(IPacket packet, IConnection connection)
    {
        if (packet == null || connection == null)
        {
            Options.Logger?.Error("Invalid packet or connection.");
            return;
        }

        ushort commandId = packet.Command;

        if (Options.PacketHandlers.TryGetValue(commandId, out Action<IPacket, IConnection>? handlerAction))
        {
            try
            {
                handlerAction.Invoke(packet, connection);
            }
            catch (Exception ex)
            {
                Options.Logger?.Error($"Error handling packet with CommandId {commandId}: {ex.Message}");
            }
        }
        else
        {
            Options.Logger?.Warn($"No handler found for CommandId {commandId}");
        }
    }
}
