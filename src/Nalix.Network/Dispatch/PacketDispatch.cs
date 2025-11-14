// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Common.Packets.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Dispatch.Options;
using Nalix.Shared.Extensions;

namespace Nalix.Network.Dispatch;

/// <summary>
/// Ultra-high performance raw dispatcher with advanced dependency injection (DI) integration and async support.
/// This implementation uses reflection to map raw command IDs to controller methods.
/// </summary>
[System.Diagnostics.DebuggerDisplay("PacketDispatch: Logger={Logger != null}")]
[System.Obsolete("Use more advanced PacketDispatchChannel with explicit handler registration instead.")]
public sealed class PacketDispatch : PacketDispatcherBase<IPacket>, IPacketDispatch<IPacket>
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
                        "Make sure to build and register IPacketCatalog before starting dispatcher.");
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void HandlePacket(IBufferLease? raw, IConnection connection)
    {
        try
        {
            // 1) Fast-fail: empty payload
            if (raw == null)
            {
                Logger?.Warn(
                    $"[{nameof(PacketDispatch)}:{nameof(HandlePacket)}] empty-payload ep={connection.EndPoint}");
                return;
            }

            // 2) Capture basic context once
            System.Int32 len = raw.Length;
            System.UInt32 magic = len >= 4 ? raw.Memory.Span.ReadMagicNumberLE() : 0u;

            // 3) Try deserialize
            if (!_catalog.TryDeserialize(raw.Span, out IPacket? packet) || packet is null)
            {
                // Log only a small head preview to avoid leaking large/secret data
                System.String head = System.Convert.ToHexString(raw.Span[..System.Math.Min(16, len)]);
                Logger?.Warn($"[{nameof(PacketDispatch)}:{nameof(HandlePacket)}] " +
                             $"deserialize-none ep={connection.EndPoint} len={len} magic=0x{magic:X8} head={head}");
                return;
            }

            // 4) Success trace (can be disabled in production)
            Logger?.Trace(
                $"[{nameof(PacketDispatch)}:{nameof(HandlePacket)}] " +
                $"deserialized ep={connection.EndPoint} type={packet.GetType().Name} len={len} magic=0x{magic:X8}");

            // 5) Dispatch to typed handler
            this.HandlePacket(packet, connection);
        }
        finally
        {
            raw?.Dispose();
        }
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
       System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void HandlePacket(IPacket packet, IConnection connection) => ExecutePacketHandlerAsync(packet, connection).Await();
}