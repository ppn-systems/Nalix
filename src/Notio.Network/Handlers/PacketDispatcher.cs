using Notio.Common.Connection;
using Notio.Common.Data;
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
    /// <inheritdoc />
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

        Options.Logger?.Debug("Deserializing packet...");
        IPacket parsedPacket = Options.DeserializationMethod(packet);
        Options.Logger?.Debug("Packet deserialized successfully.");

        this.HandlePacket(parsedPacket, connection).Wait();
    }

    /// <inheritdoc />
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

        Options.Logger?.Debug("Deserializing packet...");
        IPacket parsedPacket = Options.DeserializationMethod(packet.Value);
        Options.Logger?.Debug("Packet deserialized successfully.");

        this.HandlePacket(parsedPacket, connection).Wait();
    }

    /// <inheritdoc />
    public Task HandlePacket(IPacket? packet, IConnection connection)
    {
        if (packet == null)
        {
            Options.Logger?.Error($"No packet data provided from Ip:{connection.RemoteEndPoint}.");
            return Task.CompletedTask;
        }

        ushort commandId = packet.Command;
        Options.Logger?.Debug($"Processing packet with CommandId: {commandId}");

        if (Options.PacketHandlers.TryGetValue(commandId, out Func<IPacket, IConnection, Task>? handlerAction))
        {
            try
            {
                Options.Logger?.Debug($"Invoking handler for CommandId: {commandId}");
                return handlerAction.Invoke(packet, connection).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Options.Logger?.Error(
                            $"Error handling packet with CommandId {commandId}: {t.Exception?.GetBaseException().Message}");
                    }
                    else
                    {
                        Options.Logger?.Debug($"Handler for CommandId: {commandId} executed successfully.");
                    }
                });
            }
            catch (Exception ex)
            {
                Options.Logger?.Error($"Error handling packet with CommandId {commandId}: {ex.Message}");
                return Task.FromException(ex);
            }
        }
        else
        {
            Options.Logger?.Warn($"No handler found for CommandId {commandId}");
            return Task.CompletedTask;
        }
    }
}
