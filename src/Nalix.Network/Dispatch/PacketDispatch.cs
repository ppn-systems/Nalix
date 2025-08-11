using Nalix.Common.Connection;
using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Analyzers;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Options;
using Nalix.Shared.Extensions;

namespace Nalix.Network.Dispatch;

/// <summary>
/// Ultra-high performance raw dispatcher with advanced dependency injection (DI) integration and async support.
/// This implementation uses reflection to map raw command IDs to controller methods.
/// </summary>
/// <remarks>
/// The <see cref="PacketDispatch"/> processes incoming packets and invokes corresponding handlers
/// based on the registered command IDs. It logs errors and warnings when handling failures or unregistered commands.
/// </remarks>
/// <param name="options">
/// A delegate used to configure <see cref="PacketDispatchOptions{TPacket}"/> before processing packets.
/// </param>
public sealed class PacketDispatch(System.Action<PacketDispatchOptions<IPacket>> options)
    : PacketDispatchCore<IPacket>(options), IPacketDispatch<IPacket>
{
    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void HandlePacket(System.Byte[]? raw, IConnection connection)
    {
        if (raw == null)
        {
            base.Logger?.Warn(
                $"[Dispatch] Null System.Byte[] received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        this.HandlePacket(System.MemoryExtensions.AsSpan(raw), connection);
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void HandlePacket(
        System.ReadOnlyMemory<System.Byte>? raw,
        IConnection connection)
    {
        if (raw == null)
        {
            base.Logger?.Warn(
                "[{0}] Null ReadOnlyMemory<byte> received from {1}. Packet dropped.",
                nameof(PacketDispatch), connection.RemoteEndPoint);

            return;
        }

        this.HandlePacket(raw.Value.Span, connection);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability", "CA2012:UsePre ValueTasks correctly", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void HandlePacket(in System.ReadOnlySpan<System.Byte> raw, IConnection connection)
    {
        if (raw.IsEmpty)
        {
            base.Logger?.Error(
                "[{0}] Empty ReadOnlySpan<byte> received from {1}. Packet dropped.",
                nameof(PacketDispatch), connection.RemoteEndPoint);
            return;
        }

        System.Func<System.ReadOnlySpan<System.Byte>, IPacket>? deserializer = PacketRegistry.ResolvePacketDeserializer(raw);

        if (deserializer == null)
        {
            base.Logger?.Error("[{0}] No deserializer found for the packet from {1}. Packet dropped.",
                                nameof(PacketDispatch), connection.RemoteEndPoint);
            return;
        }

        this.HandlePacket(deserializer(raw), connection);
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void HandlePacket(IPacket packet, IConnection connection)
        => base.ExecutePacketHandlerAsync(packet, connection).Await();
}