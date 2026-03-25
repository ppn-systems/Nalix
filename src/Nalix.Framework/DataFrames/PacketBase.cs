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
using System.Threading;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Serialization;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames.Internal;
using Nalix.Framework.Extensions;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Options;
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
public abstract class PacketBase<TSelf> : FrameBase, IPoolable, IPoolRentable, IReportable, IPacketDeserializer<TSelf>, IDisposable where TSelf : PacketBase<TSelf>, new()
{
    #region Fields

    /// <summary>
    /// Indicates whether this packet was rented from a pool and should be returned on disposal.
    /// 1 = Rented, 0 = Available/Returned.
    /// </summary>
    [SerializeIgnore]
    private int _isRented;

    #endregion Fields

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

    [SerializeIgnore]
    private static readonly bool s_enablePooling = ConfigurationManager.Instance.Get<PacketOptions>().EnablePooling;

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

            // Fast path: no dynamic getters -> static size only.
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
        if (buffer.Length < s_cache.StaticSize)
        {
            throw new ArgumentException(
                $"Buffer too small: length={buffer.Length}, required>={s_cache.StaticSize}, type={typeof(TSelf).FullName}.");
        }

        try
        {
            return LiteSerializer.Serialize((TSelf)this, buffer);
        }
        catch (InvalidOperationException ex)
        {
            // Dynamic-size packets can overflow a span-backed writer even when the static portion fits.
            int required = this.Length;
            if (buffer.Length < required)
            {
                throw new ArgumentException(
                    $"Buffer too small: length={buffer.Length}, required>={required}, type={typeof(TSelf).FullName}.", ex);
            }

            throw;
        }
    }

    /// <summary>
    /// Creates or rents an instance of <typeparamref name="TSelf"/>, respecting the 
    /// <see cref="PacketOptions.EnablePooling"/> setting.
    /// </summary>
    /// <returns>A new or rented <typeparamref name="TSelf"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory pattern")]
    public static TSelf Create()
    {
        if (s_enablePooling)
        {
            return InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>().Get<TSelf>();
        }

        return new TSelf();
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
        VALIDATE_BUFFER_HEADER(buffer);

        TSelf packet = Create();
        int bytesRead = LiteSerializer.Deserialize(buffer, ref packet);

        if (bytesRead == 0)
        {
            throw new InvalidOperationException(
                $"Deserialize failed: type={typeof(TSelf).Name}, bytesRead=0, length={buffer.Length}.");
        }

        if (bytesRead < buffer.Length)
        {
            throw new SerializationFailureException(
                $"Deserialize incomplete: type={typeof(TSelf).Name}, bytesRead={bytesRead}, expected={buffer.Length}. " +
                "Potential payload corruption or trailing unconsumed data.");
        }

        return packet;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void VALIDATE_BUFFER_HEADER(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            throw new ArgumentException($"Cannot deserialize {typeof(TSelf).Name}: buffer is empty.");
        }

        if (buffer.Length < s_cache.StaticSize)
        {
            throw new SerializationFailureException(
                $"Insufficient buffer for {typeof(TSelf).Name}: length={buffer.Length}, required={s_cache.StaticSize}.");
        }

        uint bufferMagic = buffer.ReadMagicNumberLE();
        if (bufferMagic != s_autoMagic)
        {
            throw new SerializationFailureException(
                $"Magic number mismatch: type={typeof(TSelf).Name}, buffer=0x{bufferMagic:X8}, expected=0x{s_autoMagic:X8}. " +
                "The received packet type does not match the target deserialization type.");
        }
    }

    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override void ResetForPool()
    {
        Volatile.Write(ref _isRented, 0);

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
        this.Flags = PacketFlags.SYSTEM;
        this.Priority = PacketPriority.NONE;

        // Restore type identity — never reset to 0.
        this.MagicNumber = s_autoMagic;

        // Reset the rental flag so it doesn't immediately return itself when put back in pool.
        Volatile.Write(ref _isRented, 0);
    }

    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnRent() => Volatile.Write(ref _isRented, 1);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Returns rented packets to their pool once; non-rented packet instances are left alone.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        // Atomically check and clear the rented flag to ensure we only return to pool once.
        if (Interlocked.Exchange(ref _isRented, 0) == 1)
        {
            // Reset state before returning to pool
            this.ResetForPool();

            // Use the concrete type TSelf to call the fast generic Return path.
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return((TSelf)this);
        }
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
        $"{typeof(TSelf).Name}(Magic=0x{this.MagicNumber:X8}, OpCode={this.OpCode}, Flags={this.Flags}, Priority={this.Priority}, SequenceId={this.SequenceId})";

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
            // Fixed-size only when the property has no dynamic wire shape.
            // This avoids misclassifying members like string/arrays/reference-types
            // that may carry [SerializeDynamicSize(...)] as a hint.
            if (meta.DynamicKind == DynamicWireKind.None && meta.FixedSize != 0)
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
        Func<TSelf, string?> getter = BUILD_TYPED_GETTER<string?>(meta);
        int nullWireSize = meta.NullWireSize;

        return instance =>
        {
            string? value = getter(instance);
            if (value is null)
            {
                return nullWireSize; // typically 4
            }

            // StringFormatter writes an Int32 byte-count prefix followed by the UTF-8 payload.
            // Length must match the wire format exactly because Serialize(Span<byte>) relies on it.
            return sizeof(int) + Encoding.UTF8.GetByteCount(value);
        };
    }

    private static Func<TSelf, int> BUILD_BYTE_ARRAY_GETTER(PropertyMetadata meta)
    {
        Func<TSelf, byte[]?> getter = BUILD_TYPED_GETTER<byte[]?>(meta);
        int nullWireSize = meta.NullWireSize;

        return instance =>
        {
            byte[]? value = getter(instance);
            if (value is null)
            {
                return nullWireSize; // 4
            }

            return sizeof(int) + value.Length;
        };
    }

    private static Func<TSelf, int> BUILD_PACKET_GETTER(PropertyMetadata meta)
    {
        Func<TSelf, IPacket?> getter = BUILD_TYPED_GETTER<IPacket?>(meta);
        int nullWireSize = meta.NullWireSize;

        return instance =>
        {
            IPacket? packet = getter(instance);
            if (packet is null)
            {
                return nullWireSize; // 4
            }

            // Minimal self-reference guard.
            if (ReferenceEquals(packet, instance))
            {
                return 0;
            }

            // Packet properties are serialized through NullableObjectFormatter<T>,
            // which prefixes a 1-byte presence marker before the nested packet payload.
            return sizeof(byte) + packet.Length;
        };
    }

    private static Func<TSelf, int> BUILD_UNMANAGED_ARRAY_GETTER(PropertyMetadata meta)
    {
        Func<TSelf, Array?> getter = BUILD_TYPED_GETTER<Array?>(meta);
        int elementSize = meta.ElementSize;
        int nullWireSize = meta.NullWireSize;

        return instance =>
        {
            Array? value = getter(instance);
            if (value is null)
            {
                return nullWireSize; // 4
            }

            return sizeof(int) + (value.Length * elementSize);
        };
    }

    private static Func<TSelf, int> BUILD_FALLBACK_GETTER(PropertyMetadata meta)
    {
        MethodInfo buildGeneric = typeof(PacketBase<TSelf>)
            .GetMethod(nameof(BUILD_FALLBACK_GETTER_CORE), BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InternalErrorException($"Missing method: {nameof(BUILD_FALLBACK_GETTER_CORE)}");
        MethodInfo closed = buildGeneric.MakeGenericMethod(meta.DeclaredType);
        return (Func<TSelf, int>)closed.Invoke(null, [meta])!;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Func<TSelf, int> BUILD_FALLBACK_GETTER_CORE<TValue>(PropertyMetadata meta)
    {
        Func<TSelf, TValue> getter = BUILD_TYPED_GETTER<TValue>(meta);
        IFormatter<TValue> formatter = FormatterProvider.Get<TValue>();
        int nullWireSize = meta.NullWireSize;

        return instance =>
        {
            TValue value = getter(instance);
            if (value is null)
            {
                return nullWireSize;
            }

            return MEASURE_SERIALIZED_SIZE(formatter, value);
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Func<TSelf, TValue> BUILD_TYPED_GETTER<TValue>(PropertyMetadata meta)
    {
        MethodInfo? getMethod = meta.Property.GetMethod;
        if (getMethod is null)
        {
            return static _ => default!;
        }

        try
        {
            return getMethod.CreateDelegate<Func<TSelf, TValue>>();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            // Fallback for edge signatures where relaxed delegate binding cannot be created.
            return instance => (TValue)meta.GetValue(instance)!;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MEASURE_SERIALIZED_SIZE<TValue>(IFormatter<TValue> formatter, TValue value)
    {
        DataWriter writer = new(64);
        try
        {
            formatter.Serialize(ref writer, value);
            return writer.WrittenCount;
        }
        finally
        {
            writer.Dispose();
        }
    }

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
