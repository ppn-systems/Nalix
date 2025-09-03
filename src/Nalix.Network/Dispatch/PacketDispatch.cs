// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Packets.Abstractions;
using Nalix.Network.Abstractions;
using Nalix.Network.Dispatch.Options;
using Nalix.Shared.Extensions;
using Nalix.Shared.Injection;

namespace Nalix.Network.Dispatch;

/// <summary>
/// Ultra-high performance raw dispatcher with advanced dependency injection (DI) integration and async support.
/// This implementation uses reflection to map raw command IDs to controller methods.
/// </summary>

[System.Diagnostics.DebuggerDisplay("PacketDispatch: Logger={Logger != null}")]
public sealed class PacketDispatch : PacketDispatchCore<IPacket>, IPacketDispatch<IPacket>
{
    private readonly IPacketCatalog _catalog;

    /// <summary>
    /// The <see cref="PacketDispatch"/> processes incoming packets and invokes corresponding handlers
    /// based on the registered command IDs. It logs errors and warnings when handling failures or unregistered commands.
    /// </summary>
    /// <param name="options">
    /// A delegate used to configure <see cref="PacketDispatchOptions{TPacket}"/> before processing packets.
    /// </param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public PacketDispatch(System.Action<PacketDispatchOptions<IPacket>> options) : base(options)
    {
        _catalog = InstanceManager.Instance.GetExistingInstance<IPacketCatalog>()
                   ?? throw new System.InvalidOperationException(
                       $"[{nameof(PacketDispatch)}] IPacketCatalog not registered in InstanceManager. " +
                       $"Make sure to build and register IPacketCatalog before starting dispatcher.");
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void HandlePacket(System.Byte[]? raw, IConnection connection)
    {
        if (raw == null)
        {
            Logger?.Warn(
                $"[{nameof(PacketDispatch)}] " +
                $"NONE System.Byte[] received from {connection.RemoteEndPoint}. Packet dropped.");
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
                $"[{nameof(PacketDispatch)}] NONE ReadOnlyMemory<byte> received from {connection.RemoteEndPoint}. Packet dropped.");

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
                $"[{nameof(PacketDispatch)}] Empty payload from {connection.RemoteEndPoint}. Dropped.");
            return;
        }

        // 2) Capture basic context once
        System.Int32 len = raw.Length;
        System.UInt32 magic = len >= 4 ? raw.ReadMagicNumberLE() : 0u;

        // 3) Try deserialize
        if (!_catalog.TryDeserialize(raw, out IPacket? packet) || packet is null)
        {
            // Log only a small head preview to avoid leaking large/secret data
            System.String head = System.Convert.ToHexString(raw[..System.Math.Min(16, len)]);
            Logger?.Warn(
                $"[{nameof(PacketDispatch)}] Unknown packet. " +
                $"Remote={connection.RemoteEndPoint}, Len={len}, Magic=0x{magic:X8}, Head={head}. Dropped.");
            return;
        }

        // 4) Success trace (can be disabled in production)
        Logger?.Trace(
            $"[{nameof(PacketDispatch)}] Deserialized {packet.GetType().Name} from {connection.RemoteEndPoint}. " +
            $"Len={len}, Magic=0x{magic:X8}.");

        // 5) Dispatch to typed handler
        this.HandlePacket(packet, connection);
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void HandlePacket(IPacket packet, IConnection connection)
        => ExecutePacketHandlerAsync(packet, connection).Await();
}