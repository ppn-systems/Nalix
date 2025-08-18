// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Packets.Abstractions;
using Nalix.Network.Dispatch.Catalog;
using Nalix.Network.Dispatch.Core.Engine;
using Nalix.Network.Dispatch.Core.Interfaces;
using Nalix.Network.Dispatch.Options;
using Nalix.Shared.Extensions;
using Nalix.Shared.Injection;

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
[System.Diagnostics.DebuggerDisplay("PacketDispatch: Logger={Logger != null}")]
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
            Logger?.Warn(
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
            Logger?.Warn(
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
        // 1) Fast-fail: empty payload
        if (raw.IsEmpty)
        {
            Logger?.Warn(
                "[PacketDispatch] Empty payload from {0}. Dropped.",
                connection.RemoteEndPoint);
            return;
        }

        // 2) Capture basic context once
        System.Int32 len = raw.Length;
        System.UInt32 magic = len >= 4 ? raw.ReadMagicNumberLE() : 0u;

        // 3) Resolve catalog ONCE
        PacketCatalog? catalog = InstanceManager.Instance.GetExistingInstance<PacketCatalog>();
        if (catalog is null)
        {
            Logger?.Error(
                "[PacketDispatch] Missing PacketCatalog. Remote={0}, Len={1}, Magic=0x{2:X8}. Dropped.",
                connection.RemoteEndPoint, len, magic);
            return;
        }

        // 4) Try deserialize
        if (!catalog.TryDeserialize(raw, out IPacket? packet) || packet is null)
        {
            // Log only a small head preview to avoid leaking large/secret data
            System.String head = System.Convert.ToHexString(raw[..System.Math.Min(16, len)]);
            Logger?.Warn(
                "[PacketDispatch] Unknown packet. Remote={0}, Len={1}, Magic=0x{2:X8}, Head={3}. Dropped.",
                connection.RemoteEndPoint, len, magic, head);
            return;
        }

        // 5) Success trace (can be disabled in production)
        Logger?.Trace(
            "[PacketDispatch] Deserialized {0} from {1}. Len={2}, Magic=0x{3:X8}.",
            packet.GetType().Name, connection.RemoteEndPoint, len, magic);

        // 6) Dispatch to typed handler
        this.HandlePacket(packet, connection);
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void HandlePacket(IPacket packet, IConnection connection)
        => ExecutePacketHandlerAsync(packet, connection).Await();
}