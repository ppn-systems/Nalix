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
    : PacketDispatcherBase<TPacket>(options), IPacketDispatcher<TPacket>
    where TPacket : Common.Package.IPacket, Common.Package.IPacketDeserializer<TPacket>
{
    /// <inheritdoc />
    public void HandlePacket(byte[]? packet, Common.Connection.IConnection connection)
    {
        if (packet == null)
        {
            base.Logger?.Error($"[Dispatcher] Null byte[] received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        this.HandlePacket(System.MemoryExtensions.AsSpan(packet), connection);
    }

    /// <inheritdoc />
    public void HandlePacket(System.ReadOnlyMemory<byte>? packet, Common.Connection.IConnection connection)
    {
        if (packet == null)
        {
            base.Logger?.Error(
                $"[Dispatcher] Null ReadOnlyMemory<byte> received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        this.HandlePacket(packet.Value.Span, connection);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability", "CA2012:Use ValueTasks correctly", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    public void HandlePacket(in System.ReadOnlySpan<byte> packet, Common.Connection.IConnection connection)
    {
        if (packet.IsEmpty)
        {
            base.Logger?.Error(
                $"[Dispatcher] Empty ReadOnlySpan<byte> received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        this.HandlePacketAsync(TPacket.Deserialize(packet), connection);
    }

    /// <inheritdoc />
    public async void HandlePacketAsync(TPacket packet, Common.Connection.IConnection connection)
    {
        if (base.Options.TryResolveHandler(packet.Id, out var handler) && handler != null)
        {
            base.Logger?.Debug($"[Dispatcher] Dispatching packet Id: " +
                               $"{packet.Id} from {connection.RemoteEndPoint}...");

            try
            {
                await PacketDispatcher<TPacket>.ExecuteHandler(handler, packet, connection).ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                base.Logger?.Error(
                    $"[Dispatcher] Exception occurred while handling packet Id: " +
                    $"{packet.Id} from {connection.RemoteEndPoint}. " +
                    $"Error: {ex.GetType().Name} - {ex.Message}", ex);
            }

            return;
        }

        base.Logger?.Warn($"[Dispatcher] No handler found for packet Id: {packet.Id} from {connection.RemoteEndPoint}.");
    }

    private static async System.Threading.Tasks.ValueTask ExecuteHandler(
        System.Func<TPacket, Common.Connection.IConnection, System.Threading.Tasks.Task> handler,
        TPacket packet,
        Common.Connection.IConnection connection)
    {
        await handler(packet, connection).ConfigureAwait(false);
    }
}
