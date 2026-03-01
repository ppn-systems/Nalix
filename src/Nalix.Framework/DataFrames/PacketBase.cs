// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;
using Nalix.Framework.DataFrames.Internal;
using Nalix.Framework.Serialization;

namespace Nalix.Framework.DataFrames;

/// <summary>
/// Base class for all packets with automatic serialization and pooling.
/// Eliminates boilerplate code for Length, Serialize, Deserialize, and ResetForPool.
/// <para>
/// <b>MagicNumber</b> is derived automatically from <typeparamref name="TSelf"/>'s
/// full type name via FNV-1a hash — no <c>[MagicNumber]</c> attribute needed.
/// </para>
/// </summary>
/// <typeparam name="TSelf">The concrete packet type.</typeparam>
public abstract class PacketBase<TSelf> : FrameBase, IPoolable, IReportable, IPacketDeserializer<TSelf> where TSelf : PacketBase<TSelf>, new()
{
    #region Static Cache

    /// <summary>
    /// Computed once per concrete type at class-load time.
    /// </summary>
    [SerializeIgnore]
    private static readonly uint s_autoMagic = PacketRegistryFactory.Compute(typeof(TSelf));

    /// <summary>
    /// The serialization layout declared on the packet type.
    /// </summary>
    [SerializeIgnore]
    private static readonly SerializeLayout s_layout =
        typeof(TSelf).GetCustomAttribute<SerializePackableAttribute>()?.SerializeLayout ?? SerializeLayout.Auto;

    /// <summary>
    /// All serializable properties as pre-compiled PropertyMetadata.
    /// Lazy{T} guarantees thread-safe single initialization without explicit locking.
    /// Using System.Linq only at startup (inside the Lazy factory) — never in hot paths.
    /// </summary>
    [SerializeIgnore]
    private static readonly Lazy<PropertyMetadata[]> s_metadata = new(
        static () =>
        [
            .. ENUMERATE_SERIALIZABLE_PROPERTIES().Select(static x => new PropertyMetadata(x.p))
        ],
        isThreadSafe: true
    );

    /// <summary>
    /// Indicates whether <typeparamref name="TSelf"/> implements <see cref="IFixedSizeSerializable"/>.
    /// </summary>
    [SerializeIgnore]
    private static readonly bool s_isFixedSize = typeof(IFixedSizeSerializable).IsAssignableFrom(typeof(TSelf));

    /// <summary>
    /// The fixed size of the packet if it implements <see cref="IFixedSizeSerializable"/>; otherwise 0.
    /// </summary>
    [SerializeIgnore]
    private static readonly int s_fixedSize = s_isFixedSize ? FETCH_FIXED_SIZE() : 0;

    [SerializeIgnore]
    private static readonly Cache s_cache = InitializeCache();

    #endregion Static Cache

    #region Constructor

    /// <summary>
    /// Assigns the automatically derived <see cref="FrameBase.MagicNumber"/>
    /// so that every packet is self-identifying on the wire without any attribute.
    /// </summary>
    protected PacketBase() => this.MagicNumber = s_autoMagic;

    #endregion Constructor

    #region Length

    /// <inheritdoc/>
    [SerializeIgnore]
    public override int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // O(1) path for fixed-size serializable packets.
            if (s_isFixedSize)
            {
                return s_fixedSize;
            }

            // Fast path: no dynamic getters -> fixed size.
            Func<TSelf, int>[] getters = s_cache.SizeGetters;
            if (getters.Length == 0)
            {
                return s_cache.StaticSize;
            }

            int size = s_cache.StaticSize;
            TSelf self = (TSelf)this;

            // Tight loop: just delegate calls + integer adds.
            for (int i = 0; i < getters.Length; i++)
            {
                size += getters[i](self);
            }

            return size;
        }
    }

    #endregion Length

    #region APIs

    /// <inheritdoc/>
    /// <exception cref="SerializationFailureException">Thrown when the packet cannot be serialized by the configured formatter.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no formatter is available for the packet type.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override byte[] Serialize() => LiteSerializer.Serialize((TSelf)this);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">Thrown when <paramref name="buffer"/> is too small for the serialized packet.</exception>
    /// <exception cref="SerializationFailureException">Thrown when the packet cannot be serialized by the configured formatter.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no formatter is available for the packet type.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Serialize(Span<byte> buffer)
    {
        int length = this.Length;
        if (buffer.Length < length)
        {
            throw new ArgumentException(
                $"Buffer too small: length={buffer.Length}, required>={length}, type={typeof(TSelf).FullName}.");
        }

        return LiteSerializer.Serialize((TSelf)this, buffer);
    }

    /// <summary>
    /// Deserializes a <typeparamref name="TSelf"/> packet from <paramref name="buffer"/>
    /// using object pooling to avoid heap allocation.
    /// </summary>
    /// <param name="buffer">The raw wire bytes to deserialize from.</param>
    /// <returns>A <typeparamref name="TSelf"/> instance populated from the buffer.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="buffer"/> is empty.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when deserialization reads zero bytes or no formatter is available for the packet type.
    /// </exception>
    /// <exception cref="SerializationFailureException">
    /// Thrown when the payload is malformed or does not contain enough data to deserialize the packet.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "<Pending>")]
    public static TSelf Deserialize(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            throw new ArgumentException(
                $"Cannot deserialize {typeof(TSelf).Name}: buffer is empty.");
        }

        TSelf packet = new();

        int bytesRead = LiteSerializer.Deserialize(buffer, ref packet);

        if (bytesRead == 0)
        {
            throw new InvalidOperationException(
                $"Deserialize failed: type={typeof(TSelf).Name}, bytesRead=0, length={buffer.Length}.");
        }

        return packet;
    }

    /// <inheritdoc/>    
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="buffer"/> is empty.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when deserialization reads zero bytes or no formatter is available for the packet type.
    /// </exception>
    /// <exception cref="SerializationFailureException">
    /// Thrown when the payload is malformed or does not contain enough data to deserialize the packet.
    /// </exception>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "<Pending>")]
    public static TSelf Deserialize(ReadOnlyMemory<byte> buffer, ref TSelf value)
    {
        if (buffer.IsEmpty)
        {
            throw new ArgumentException(
                $"Cannot deserialize {typeof(TSelf).Name}: buffer is empty.");
        }

        int bytesRead = LiteSerializer.Deserialize(buffer.Span, ref value);
        if (bytesRead == 0)
        {
            throw new InvalidOperationException(
                $"Deserialize failed: type={typeof(TSelf).Name}, bytesRead=0, length={buffer.Length}.");
        }
        return value;
    }

    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override void ResetForPool()
    {
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
        this.OpCode = 0;
        this.Flags = PacketFlags.NONE;
        this.Protocol = ProtocolType.NONE;
        this.Priority = PacketPriority.NONE;

        // Restore type identity — never reset to 0.
        this.MagicNumber = s_autoMagic;
    }

    #endregion APIs

    #region Diagnostics

    /// <summary>
    /// Returns a debug-friendly description of this packet's metadata.
    /// Not intended for production logging — allocates strings.
    /// </summary>
    /// <exception cref="FormatException">Thrown when diagnostic formatting of packet metadata fails.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string GenerateReport()
    {
        StringBuilder sb = new(128);

        _ = sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"[{typeof(TSelf).Name}] s_autoMagic=0x{s_autoMagic:X8} StaticSize={s_cache.StaticSize} " +
            $"Properties={s_cache.All.Length} DynamicGetters={s_cache.SizeGetters.Length}");

        foreach (PropertyMetadata meta in s_cache.All)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"  {meta}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns a debug-friendly key-value summary of this packet's metadata (for diagnostics, not for production use).
    /// </summary>
    /// <exception cref="FormatException">Thrown when diagnostic formatting of packet metadata fails.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IDictionary<string, object> GetReportData()
    {
        return new Dictionary<string, object>
        {
            ["TypeName"] = typeof(TSelf).Name,
            ["AutoMagic"] = $"0x{s_autoMagic:X8}",
            ["StaticSize"] = s_cache.StaticSize.ToString(CultureInfo.InvariantCulture),
            ["PropertiesCount"] = s_cache.All.Length,
            ["DynamicGettersCount"] = s_cache.SizeGetters.Length,
            ["Properties"] = Array.ConvertAll(s_cache.All, meta => meta.ToString())
        };
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"{typeof(TSelf).Name}(Magic=0x{this.MagicNumber:X8}, OpCode={this.OpCode}, Flags={this.Flags}, Priority={this.Priority}, Protocol={this.Protocol})";

    #endregion Diagnostics

    #region Private Methods

    private readonly struct Cache
    {
        public readonly int StaticSize;
        public readonly PropertyMetadata[] All;
        public readonly Func<TSelf, int>[] SizeGetters;

        public Cache(PropertyMetadata[] all, int staticSize, Func<TSelf, int>[] sizeGetters)
        {
            All = all;
            StaticSize = staticSize;
            SizeGetters = sizeGetters;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Cache InitializeCache()
    {
        PropertyMetadata[] all = s_metadata.Value;

        // If the packet is explicitly marked as fixed-size, skip per-property discovery
        // and use the provided size. Properties are still tracked for ResetForPool/Reports.
        if (s_isFixedSize)
        {
            return new Cache(all, s_fixedSize, Array.Empty<Func<TSelf, int>>());
        }

        int staticSize = PacketConstants.HeaderSize;
        List<Func<TSelf, int>> getters = new(capacity: all.Length);

        foreach (PropertyMetadata meta in all)
        {
            // Fixed-size: pre-sum into staticSize and never touch getter in Length().
            if (!meta.IsDynamic && meta.FixedSize != 0)
            {
                staticSize += meta.FixedSize;
                continue;
            }

            // Dynamic or unknown-size => build a size getter.
            // This keeps Length() a tight loop over delegates.
            getters.Add(BUILD_SIZE_GETTER(meta));
        }

        return new Cache(all, staticSize, [.. getters]);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Func<TSelf, int> BUILD_SIZE_GETTER(PropertyMetadata meta)
    {
        // Localize meta reference for closure; this closure is created once per property per TSelf.
        // The returned delegate MUST be allocation-free per invocation.
        return meta.DynamicKind switch
        {
            DynamicWireKind.String => BUILD_STRING_GETTER(meta),
            DynamicWireKind.ByteArray => BUILD_BYTE_ARRAY_GETTER(meta),
            DynamicWireKind.Packet => BUILD_PACKET_GETTER(meta),
            DynamicWireKind.UnmanagedArray => BUILD_UNMANAGED_ARRAY_GETTER(meta),
            DynamicWireKind.None or DynamicWireKind.Other => BUILD_FALLBACK_GETTER(meta),
            _ => BUILD_FALLBACK_GETTER(meta),
        };
    }

    private static Func<TSelf, int> BUILD_STRING_GETTER(PropertyMetadata meta)
    {
        return instance =>
        {
            object? value = meta.GetValue(instance);

            if (value is null)
            {
                return meta.NullWireSize; // typically 4
            }

            // StringFormatter writes an Int32 byte-count prefix followed by the UTF-8 payload.
            // Length must match the wire format exactly because Serialize(Span<byte>) relies on it.
            return sizeof(int) + Encoding.UTF8.GetByteCount((string)value);
        };
    }

    private static Func<TSelf, int> BUILD_BYTE_ARRAY_GETTER(PropertyMetadata meta)
    {
        return instance =>
        {
            object? value = meta.GetValue(instance);
            if (value is null)
            {
                return meta.NullWireSize; // 4
            }

            return sizeof(int) + ((byte[])value).Length;
        };
    }

    private static Func<TSelf, int> BUILD_PACKET_GETTER(PropertyMetadata meta)
    {
        return instance =>
        {
            object? value = meta.GetValue(instance);
            if (value is null)
            {
                return meta.NullWireSize; // 4
            }

            IPacket p = (IPacket)value;

            // Minimal self-reference guard.
            if (ReferenceEquals(p, instance))
            {
                return 0;
            }

            // Packet properties are serialized through NullableObjectFormatter<T>,
            // which prefixes a 1-byte presence marker before the nested packet payload.
            return sizeof(byte) + p.Length;
        };
    }

    private static Func<TSelf, int> BUILD_UNMANAGED_ARRAY_GETTER(PropertyMetadata meta)
    {
        int elementSize = meta.ElementSize;

        return instance =>
        {
            object? value = meta.GetValue(instance);
            if (value is null)
            {
                return meta.NullWireSize; // 4
            }

            Array arr = (Array)value;
            return sizeof(int) + (arr.Length * elementSize);
        };
    }

    private static Func<TSelf, int> BUILD_FALLBACK_GETTER(PropertyMetadata meta)
    {
        return instance =>
        {
            object? value = meta.GetValue(instance);
            if (value is null)
            {
                return meta.NullWireSize;
            }

            // Conservative runtime sizing for uncommon types.
            return GET_DYNAMIC_SIZE_FALLBACK(instance, value);
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GET_DYNAMIC_SIZE_FALLBACK(PacketBase<TSelf> self, object value)
    {
        // NOTE: keep this only for rare cases.
        if (value is string s)
        {
            return string.IsNullOrEmpty(s) ? sizeof(int) : sizeof(int) + Encoding.UTF8.GetByteCount(s);
        }

        if (value is byte[] b)
        {
            return sizeof(int) + b.Length;
        }

        if (value is IPacket p)
        {
            if (ReferenceEquals(p, self))
            {
                return 0;
            }

            return p.Length;
        }

        if (value is Array arr)
        {
            Type? elementType = arr.GetType().GetElementType();
            if (elementType is not null && Serialization.Internal.Types.TypeMetadata.IsUnmanaged(elementType))
            {
                int elementSize = PacketBaseElementSizer.GetElementSize(elementType);
                return sizeof(int) + (arr.Length * elementSize);
            }
        }

        throw new SerializationFailureException($"Unsupported dynamic property type: {value.GetType().FullName}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GET_DYNAMIC_SIZE_FALLBACK(TSelf instance, object value) => PacketBase<TSelf>.GET_DYNAMIC_SIZE_FALLBACK((PacketBase<TSelf>)instance, value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<(PropertyInfo p, SerializeOrderAttribute? order)> ENUMERATE_SERIALIZABLE_PROPERTIES()
    {
        IEnumerable<(PropertyInfo p, SerializeOrderAttribute? order, SerializeIgnoreAttribute? ignore, SerializeHeaderAttribute? header)> candidates =
            typeof(TSelf)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(static p => (
                    p,
                    order: p.GetCustomAttribute<SerializeOrderAttribute>(),
                    ignore: p.GetCustomAttribute<SerializeIgnoreAttribute>(),
                    header: p.GetCustomAttribute<SerializeHeaderAttribute>()));

        IEnumerable<(PropertyInfo p, SerializeOrderAttribute? order)> selected = candidates
            .Where(static x => x.ignore is null && x.header is null)
            .Select(static x => (x.p, x.order));

        return s_layout == SerializeLayout.Explicit
            ? selected
                .Where(static x => x.order is not null)
                .OrderBy(static x => x.order!.Order)
            : selected;
    }

    private static int FETCH_FIXED_SIZE()
    {
        return typeof(TSelf).GetProperty(nameof(IFixedSizeSerializable.Size), BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null) is int size ? size : 0;
    }

    #endregion Private Methods
}
