namespace Nalix.Network.Dispatch;

/// <summary>
/// Ultra-high performance packet dispatcher with advanced dependency injection (DI) integration and async support.
/// This implementation uses reflection to map packet command IDs to controller methods.
/// </summary>
/// <remarks>
/// The <see cref="PacketDispatch{TPacket}"/> processes incoming packets and invokes corresponding handlers
/// based on the registered command IDs. It logs errors and warnings when handling failures or unregistered commands.
/// </remarks>
/// <param name="options">
/// A delegate used to configure <see cref="Options.PacketDispatchOptions{TPacket}"/> before processing packets.
/// </param>
public sealed class PacketDispatch<TPacket>(System.Action<Options.PacketDispatchOptions<TPacket>> options)
    : PacketDispatchCore<TPacket>(options), IPacketDispatch<TPacket> where TPacket : Common.Package.IPacket,
    Common.Package.IPacketEncryptor<TPacket>,
    Common.Package.IPacketCompressor<TPacket>,
    Common.Package.IPacketDeserializer<TPacket>
{
    /// <inheritdoc />
    public void HandlePacket(byte[]? packet, Common.Connection.IConnection connection)
    {
        if (packet == null)
        {
            base.Logger?.Error($"[Dispatch] Null byte[] received from {connection.RemoteEndPoint}. Packet dropped.");
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
                "[{0}] Null ReadOnlyMemory<byte> received from {1}. Packet dropped.",
                nameof(PacketDispatch<TPacket>), connection.RemoteEndPoint);
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
                "[{0}] Empty ReadOnlySpan<byte> received from {1}. Packet dropped.",
                nameof(PacketDispatch<TPacket>), connection.RemoteEndPoint);
            return;
        }

        this.HandlePacketAsync(TPacket.Deserialize(packet), connection);
    }

    /// <inheritdoc />
    public async void HandlePacketAsync(TPacket packet, Common.Connection.IConnection connection)
        => await base.ExecutePacketHandlerAsync(packet, connection);
}
