// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;
using Nalix.Shared.Frames.Internal;
using Nalix.Shared.Memory.Objects;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Frames;

/// <summary>
/// Base class for all packets with automatic serialization and pooling.
/// Eliminates boilerplate code for Length, Serialize, Deserialize, and ResetForPool.
/// <para>
/// <b>MagicNumber</b> is derived automatically from <typeparamref name="TSelf"/>'s
/// full type name via FNV-1a hash — no <c>[MagicNumber]</c> attribute needed.
/// </para>
/// </summary>
public abstract class PacketBase<TSelf> : FrameBase, IPoolable, IReportable, IPacketDeserializer<TSelf>
    where TSelf : PacketBase<TSelf>, new()
{
    #region Static Cache

    // Computed once per concrete type at class-load time.
    private static readonly System.UInt32 s_autoMagic = PacketRegistryFactory.Compute(typeof(TSelf));

    // All serializable properties as pre-compiled PropertyMetadata.
    // Lazy<T> guarantees thread-safe single initialization without explicit locking.
    // Using System.Linq only at startup (inside the Lazy factory) — never in hot paths.
    private static readonly System.Lazy<PropertyMetadata[]> s_metadata = new(
        static () =>
        [
            .. System.Linq.Enumerable.Select(
                System.Linq.Enumerable.OrderBy(
                    System.Linq.Enumerable.Where(
                        System.Linq.Enumerable.Select(
                            typeof(TSelf).GetProperties(
                                System.Reflection.BindingFlags.Public |
                                System.Reflection.BindingFlags.Instance),
                            static p => (
                                p,
                                order: System.Reflection.CustomAttributeExtensions
                                           .GetCustomAttribute<SerializeOrderAttribute>(p),
                                ignore: System.Reflection.CustomAttributeExtensions
                                            .GetCustomAttribute<SerializeIgnoreAttribute>(p)
                            )
                        ),
                        // Both conditions evaluated with the already-fetched attributes
                        // — no second GetCustomAttribute scan.
                        static x => x.order is not null && x.ignore is null
                    ),
                    static x => x.order!.Order
                ),
                static x => new PropertyMetadata(x.p)
            )
        ],
        isThreadSafe: true
    );

    // null  → has dynamic properties, call ComputeDynamicLength() at runtime.
    // value → all properties are fixed-size, return directly.
    // Using ushort? avoids the "0-as-sentinel" ambiguity from the previous version.
    private static readonly System.Lazy<System.UInt16?> _cachedFixedSize = new(
        static () =>
        {
            System.UInt16 size = PacketConstants.HeaderSize;
            foreach (PropertyMetadata meta in s_metadata.Value)
            {
                if (meta.IsDynamic)
                {
                    return null; // signal: at least one property needs runtime measurement
                }

                size += meta.FixedSize;
            }
            return size;
        },
        isThreadSafe: true
    );

    // Cached ObjectPoolManager reference — avoids two GetOrCreateInstance() calls
    // per Deserialize() invocation. Resolved lazily on first packet deserialization.
    private static readonly System.Lazy<ObjectPoolManager> _pool = new(
        static () => InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>(),
        isThreadSafe: true
    );

    #endregion Static Cache

    #region Constructor

    /// <summary>
    /// Assigns the automatically derived <see cref="FrameBase.MagicNumber"/>
    /// so that every packet is self-identifying on the wire without any attribute.
    /// </summary>
    protected PacketBase() => MagicNumber = s_autoMagic;

    #endregion Constructor

    #region Length

    /// <inheritdoc/>
    [SerializeIgnore]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public override System.UInt16 Length
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get
        {
            // Fast path: all properties are fixed-size → return cached value directly.
            System.UInt16? fixedSize = _cachedFixedSize.Value;
            return fixedSize.HasValue ? fixedSize.Value : ComputeDynamicLength();
        }
    }

    /// <summary>
    /// Walks all properties to compute the actual wire-length at runtime.
    /// Fixed-size contributions use the cached <see cref="PropertyMetadata.FixedSize"/>;
    /// dynamic contributions call through to the compiled getter delegate.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private System.UInt16 ComputeDynamicLength()
    {
        System.UInt16 size = PacketConstants.HeaderSize;

        foreach (PropertyMetadata meta in s_metadata.Value)
        {
            if (!meta.IsDynamic)
            {
                size += meta.FixedSize;
                continue;
            }

            // Dynamic: measure actual content at runtime.
            // string: UTF-8 byte count + 2-byte length prefix (matches LiteSerializer wire format).
            // byte[]: raw byte count + 4-byte length prefix.
            // Unknown dynamic type: contributes 0 — subclass should override if needed.
            size += meta.GetValue(this) switch
            {
                System.String str when str.Length > 0
                    => (System.UInt16)(System.Text.Encoding.UTF8.GetByteCount(str) + sizeof(System.UInt16)),

                System.String _       // empty string: only the 2-byte prefix
                    => sizeof(System.UInt16),

                System.Byte[] { Length: > 0 } bytes
                    => (System.UInt16)(bytes.Length + sizeof(System.Int32)),

                System.Byte[] _       // empty array: only the 4-byte prefix
                    => sizeof(System.Int32),

                _ => 0
            };
        }

        return size;
    }

    #endregion Length

    #region APIs

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.Byte[] Serialize() => LiteSerializer.Serialize<TSelf>((TSelf)this);

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.Int32 Serialize(System.Span<System.Byte> buffer)
    {
        System.UInt16 required = Length;
        return buffer.Length < required
            ? throw new System.ArgumentException(
                $"Buffer too small for {typeof(TSelf).Name}. " +
                $"Required: {required}, Actual: {buffer.Length}.",
                nameof(buffer))
            : LiteSerializer.Serialize<TSelf>((TSelf)this, buffer);
    }

    /// <summary>
    /// Deserializes a <typeparamref name="TSelf"/> packet from <paramref name="buffer"/>
    /// using object pooling to avoid heap allocation.
    /// </summary>
    /// <param name="buffer">The raw wire bytes to deserialize from.</param>
    /// <returns>A <typeparamref name="TSelf"/> instance populated from the buffer.</returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="buffer"/> is empty.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when deserialization reads zero bytes (corrupt or truncated frame).
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TSelf Deserialize(System.ReadOnlySpan<System.Byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            throw new System.ArgumentException(
                $"Cannot deserialize {typeof(TSelf).Name} from an empty buffer.",
                nameof(buffer));
        }
        TSelf packet = new();

        System.Int32 bytesRead = LiteSerializer.Deserialize(buffer, ref packet);

        return bytesRead == 0
            ? throw new System.InvalidOperationException(
                $"Failed to deserialize {typeof(TSelf).Name}: zero bytes were consumed. " +
                $"Buffer length: {buffer.Length}.")
            : packet;
    }

    /// <inheritdoc/>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public override void ResetForPool()
    {
        MagicNumber = s_autoMagic; // Restore type identity — never reset to 0.

        // Reset all user-defined serializable properties via compiled delegates.
        // No GetCustomAttribute calls in this path.
        foreach (PropertyMetadata meta in s_metadata.Value)
        {
            if (meta.IsWritable)
            {
                meta.SetValue(this, meta.DefaultValue);
            }
        }

        // Explicitly reset all FrameBase header fields to well-known defaults.
        // These are declared in the base class so _metadata may or may not include them
        // depending on whether SerializeOrder is defined — reset them unconditionally.
        OpCode = 0;
        Flags = PacketFlags.NONE;
        Protocol = ProtocolType.NONE;
        Priority = PacketPriority.NONE;
    }

    #endregion APIs

    #region Diagnostics

    /// <summary>
    /// Returns a debug-friendly description of this packet's metadata.
    /// Not intended for production logging — allocates strings.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public System.String GenerateReport()
    {
        System.Text.StringBuilder sb = new(128);
        sb.AppendLine($"[{typeof(TSelf).Name}] s_autoMagic=0x{s_autoMagic:X8} FixedSize={_cachedFixedSize.Value?.ToString() ?? "dynamic"} Properties={s_metadata.Value.Length}");

        foreach (PropertyMetadata meta in s_metadata.Value)
        {
            sb.AppendLine($"  {meta}");
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public override System.String ToString() => $"{typeof(TSelf).Name}(Magic=0x{MagicNumber:X8}, OpCode={OpCode}, Flags={Flags}, Priority={Priority}, Protocol={Protocol})";

    #endregion Diagnostics
}