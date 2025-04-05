namespace Notio.Network.Dispatcher;

/// <summary>
/// Ultra-high performance packet dispatcher with advanced dependency injection (DI) integration and async support.
/// This implementation uses reflection to map packet command IDs to controller methods.
/// </summary>
/// <remarks>
/// The <see cref="PacketDispatcher{TPacket}"/> processes incoming packets and invokes corresponding handlers
/// based on the registered command IDs. It logs errors and warnings when handling failures or unregistered commands.
/// </remarks>
/// <param name="options">
/// A delegate used to configure <see cref="Options.PacketDispatcherOptions{TPacket}"/> before processing packets.
/// </param>
public sealed class PacketDispatcher<TPacket>(System.Action<Options.PacketDispatcherOptions<TPacket>> options)
    : PacketDispatcherBase<TPacket>(options), IPacketDispatcher<TPacket> where TPacket : Common.Package.IPacket
{
    /// <inheritdoc />
    public void HandlePacket(byte[]? packet, Common.Connection.IConnection connection)
    {
        if (packet == null)
        {
            Logger?.Error($"No packet data provided from Ip: {connection.RemoteEndPoint}.");
            return;
        }

        HandlePacket(Options.Deserialize(packet), connection).Wait();
    }

    /// <inheritdoc />
    public void HandlePacket(System.ReadOnlyMemory<byte>? packet, Common.Connection.IConnection connection)
    {
        if (packet == null)
        {
            Logger?.Error($"No packet data provided from Ip: {connection.RemoteEndPoint}.");
            return;
        }

        HandlePacket(Options.Deserialize(packet), connection).Wait();
    }

    /// <inheritdoc />
    public async System.Threading.Tasks.Task HandlePacket(TPacket? packet, Common.Connection.IConnection connection)
    {
        if (packet == null)
        {
            Logger?.Error($"No packet data provided from Ip: {connection.RemoteEndPoint}.");
            return;
        }

        if (Options.TryResolveHandler(packet.Id, out var handler))
        {
            Logger?.Debug($"Invoking handler for Number: {packet.Id}");

            try
            {
                await handler!(packet, connection).ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                Logger?.Error($"Error handling packet with Number {packet.Id}: {ex.Message}", ex);
            }
        }
        else
        {
            Logger?.Warn($"No handler found for Number {packet.Id}");
        }
    }
}
