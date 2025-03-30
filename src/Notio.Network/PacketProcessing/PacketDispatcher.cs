using Notio.Common.Connection;
using Notio.Common.Package;
using Notio.Network.PacketProcessing.Options;
using System;
using System.Threading.Tasks;

namespace Notio.Network.PacketProcessing;

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
public sealed class PacketDispatcher(Action<PacketDispatcherOptions> options)
    : PacketDispatcherBase(options), IPacketDispatcher
{
    /// <inheritdoc />
    public void HandlePacket(byte[]? packet, IConnection connection)
    {
        if (packet == null)
        {
            Logger?.Error($"No packet data provided from Ip: {connection.RemoteEndPoint}.");
            return;
        }

        HandlePacket(Options.Deserialization(packet), connection).Wait();
    }

    /// <inheritdoc />
    public void HandlePacket(ReadOnlyMemory<byte>? packet, IConnection connection)
    {
        if (packet == null)
        {
            Logger?.Error($"No packet data provided from Ip: {connection.RemoteEndPoint}.");
            return;
        }

        HandlePacket(Options.Deserialization(packet), connection).Wait();
    }

    /// <inheritdoc />
    public async Task HandlePacket(IPacket? packet, IConnection connection)
    {
        if (packet == null)
        {
            Logger?.Error($"No packet data provided from Ip: {connection.RemoteEndPoint}.");
            return;
        }

        ushort commandId = packet.Command;

        if (Options.TryGetPacketHandler(commandId, out var handler))
        {
            Logger?.Debug($"Invoking handler for CommandId: {commandId}");

            try
            {
                await handler!(packet, connection).ConfigureAwait(false);
            }
            catch (Exception ex)
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
