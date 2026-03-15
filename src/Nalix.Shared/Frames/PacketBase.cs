// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Caching;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Packets.Enums;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security.Enums;
using Nalix.Common.Serialization.Attributes;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Registry;
using Nalix.Shared.Security;
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
public abstract class PacketBase<TSelf> : FrameBase, IPoolable, IPacketDeserializer<TSelf>
    where TSelf : PacketBase<TSelf>, new()
{
    #region Static Cache

    // Computed once per concrete type at class-load time.
    private static readonly System.UInt32 AutoMagic =
        PacketRegistryFactory.Compute(typeof(TSelf));

    // All serializable properties as pre-compiled PropertyMetadata.
    // Lazy<T> guarantees thread-safe single initialization without explicit locking.
    // Using System.Linq only at startup (inside the Lazy factory) — never in hot paths.
    private static readonly System.Lazy<PropertyMetadata[]> _metadata = new(
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
            foreach (PropertyMetadata meta in _metadata.Value)
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
    protected PacketBase() => MagicNumber = AutoMagic;

    #endregion Constructor

    #region Length

    /// <inheritdoc/>
    [SerializeIgnore]
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

        foreach (PropertyMetadata meta in _metadata.Value)
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
    public override System.Byte[] Serialize()
        => LiteSerializer.Serialize<TSelf>((TSelf)this);

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

        // Single pool reference — no double GetOrCreateInstance() call.
        ObjectPoolManager pool = _pool.Value;
        TSelf packet = pool.Get<TSelf>();

        try
        {
            System.Int32 bytesRead = LiteSerializer.Deserialize(buffer, ref packet);

            return bytesRead == 0
                ? throw new System.InvalidOperationException(
                    $"Failed to deserialize {typeof(TSelf).Name}: zero bytes were consumed. " +
                    $"Buffer length: {buffer.Length}.")
                : packet;
        }
        catch
        {
            // Return the leased instance to the pool before propagating any exception
            // — prevents pool exhaustion on corrupt/malformed frames.
            pool.Return(packet);
            throw;
        }
    }

    /// <summary>
    /// Attempts to deserialize a <typeparamref name="TSelf"/> packet without throwing.
    /// </summary>
    /// <param name="buffer">The raw wire bytes.</param>
    /// <param name="packet">
    /// When this method returns <see langword="true"/>, the deserialized packet;
    /// otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success; <see langword="false"/> on any failure.
    /// </returns>
    public static System.Boolean TryDeserialize(
        System.ReadOnlySpan<System.Byte> buffer,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TSelf? packet)
    {
        try
        {
            packet = Deserialize(buffer);
            return true;
        }
        catch
        {
            packet = null;
            return false;
        }
    }

    /// <summary>
    /// Encrypts the provided packet using the specified symmetric key and cipher suite.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TSelf Encrypt(TSelf packet, System.Byte[] key, CipherSuiteType algorithm)
        => EnvelopeEncryptor.Encrypt<TSelf>(packet, key, algorithm);

    /// <summary>
    /// Decrypts the provided packet using the specified symmetric key.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TSelf Decrypt(TSelf packet, System.Byte[] key)
        => EnvelopeEncryptor.Decrypt<TSelf>(packet, key);

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        // Reset all user-defined serializable properties via compiled delegates.
        // No GetCustomAttribute calls in this path.
        foreach (PropertyMetadata meta in _metadata.Value)
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
        MagicNumber = AutoMagic; // Restore type identity — never reset to 0.
    }

    #endregion APIs

    #region Diagnostics

    /// <summary>
    /// Returns a debug-friendly description of this packet's metadata.
    /// Not intended for production logging — allocates strings.
    /// </summary>
    public System.String DetailsMetadata()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[{typeof(TSelf).Name}] AutoMagic=0x{AutoMagic:X8} " +
                      $"FixedSize={_cachedFixedSize.Value?.ToString() ?? "dynamic"} " +
                      $"Properties={_metadata.Value.Length}");

        foreach (PropertyMetadata meta in _metadata.Value)
        {
            sb.AppendLine($"  {meta}");
        }

        return sb.ToString();
    }

    #endregion Diagnostics
}