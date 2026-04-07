// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
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
using Nalix.Framework.Serialization.Internal.Types;

namespace Nalix.Framework.DataFrames;

/// <summary>
/// Base class for all packets with automatic serialization and pooling.
/// Eliminates boilerplate code for Length, Serialize, Deserialize, and ResetForPool.
/// <para>
/// <b>MagicNumber</b> is derived automatically from <typeparamref name="TSelf"/>'s
/// full type name via FNV-1a hash — no <c>[MagicNumber]</c> attribute needed.
/// </para>
/// </summary>
/// <typeparam name="TSelf"></typeparam>
public abstract class PacketBase<TSelf> : FrameBase, IPoolable, IReportable, IPacketDeserializer<TSelf>
    where TSelf : PacketBase<TSelf>, new()
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
    private static readonly SerializeLayout s_layout = typeof(TSelf).GetCustomAttribute<SerializePackableAttribute>()?.SerializeLayout ?? SerializeLayout.Auto;

    /// <summary>
    /// All serializable properties as pre-compiled PropertyMetadata.
    /// Lazy{T} guarantees thread-safe single initialization without explicit locking.
    /// Using System.Linq only at startup (inside the Lazy factory) — never in hot paths.
    /// </summary>
    [SerializeIgnore]
    private static readonly Lazy<PropertyMetadata[]> s_metadata = new(
        static () =>
        [
            .. Enumerable.Select(
                EnumerateSerializableProperties(),
                static x => new PropertyMetadata(x.p)
            )
        ],
        isThreadSafe: true
    );

    /// <summary>
    /// null  -> has dynamic properties, call ComputeDynamicLength() at runtime.
    /// value -> all properties are fixed-size, return directly.
    /// Using int? avoids the "0-as-sentinel" ambiguity from the previous version.
    /// </summary>
    [SerializeIgnore]
    private static readonly Lazy<int?> s_cachedFixedSize = new(
        static () =>
        {
            int size = PacketConstants.HeaderSize;
            foreach (PropertyMetadata meta in s_metadata.Value)
            {
                if (meta.IsDynamic || meta.FixedSize == 0)
                {
                    return null; // signal: at least one property needs runtime measurement
                }

                size += meta.FixedSize;
            }
            return size;
        },
        isThreadSafe: true
    );

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
            // Fast path: all properties are fixed-size -> return cached value directly.
            int? fixedSize = s_cachedFixedSize.Value;
            return fixedSize ?? this.COMPUTE_DYNAMIC_LENGTH();
        }
    }

    /// <summary>
    /// Walks all properties to compute the actual wire-length at runtime.
    /// Fixed-size contributions use the cached <see cref="PropertyMetadata.FixedSize"/>;
    /// dynamic contributions call through to the compiled getter delegate.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int COMPUTE_DYNAMIC_LENGTH()
    {
        int size = PacketConstants.HeaderSize;

        foreach (PropertyMetadata meta in s_metadata.Value)
        {
            object? value = meta.GetValue(this);
            if (value is null)
            {
                continue;
            }

            if (meta.IsDynamic)
            {
                size += this.GetDynamicSize(value);
                continue;
            }

            if (meta.FixedSize != 0)
            {
                size += meta.FixedSize;
                continue;
            }

            // Reference types without [SerializeDynamicSize] (like string, byte[], nested IPacket)
            size += this.GetDynamicSize(value);
        }

        return size;
    }

    /// <summary>
    /// Cache Func&lt;int&gt; delegate cho Unsafe.SizeOf&lt;T&gt;() theo runtime Type.
    /// Chỉ được truy cập qua <see cref="GetElementSize"/> — tránh reflection lặp lại.
    /// </summary>
    [SerializeIgnore]
    private static readonly ConcurrentDictionary<Type, Func<int>> s_elementSizeCache = new();

    /// <summary>
    /// Tính wire-size của một giá trị động, đồng bộ với format của <see cref="LiteSerializer"/>:
    /// <list type="bullet">
    ///   <item><see cref="string"/>  — 2-byte prefix + UTF-8 bytes (xác nhận từ <c>GetExactLengthOrThrow</c>)</item>
    ///   <item><see cref="byte"/>[]  — 4-byte int prefix + raw bytes (xác nhận từ <c>UnmanagedSZArray</c> path)</item>
    ///   <item><see cref="IPacket"/> — delegate sang <c>p.Length</c>, có guard tự-tham chiếu</item>
    ///   <item>Unmanaged array       — 4-byte int prefix + elements (cùng path với byte[])</item>
    /// </list>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetDynamicSize(object value)
    {
        // string: 2-byte ushort prefix + UTF-8 payload.
        // Đồng bộ với LiteSerializer.GetExactLengthOrThrow và TypeMetadata.GetDynamicSize.
        if (value is string s)
        {
            return string.IsNullOrEmpty(s) ? 2 : 2 + Encoding.UTF8.GetByteCount(s);
        }

        // byte[]: 4-byte int prefix + raw bytes.
        // LiteSerializer viết: GC.AllocateUninitializedArray<byte>(dataSize + 4)
        // Trước đây dùng sizeof(ushort)=2 — SAI, gây tính Length thiếu 2 bytes mỗi byte[].
        if (value is byte[] b)
        {
            return sizeof(int) + b.Length;
        }

        if (value is IPacket p)
        {
            // Guard tự-tham chiếu cơ bản — cycle detection đầy đủ quá tốn kém cho hot path.
            if (ReferenceEquals(p, this))
            {
                return 0;
            }

            return p.Length;
        }

        if (value is Array arr)
        {
            // Unmanaged array (int[], float[], ...): 4-byte int prefix + elements.
            // Cùng format với byte[] trong LiteSerializer (UnmanagedSZArray path).
            Type? elementType = arr.GetType().GetElementType();
            if (elementType is not null && TypeMetadata.IsUnmanaged(elementType))
            {
                return sizeof(int) + (arr.Length * GetElementSize(elementType));
            }
        }

        return 0;
    }

    /// <summary>
    /// Trả về byte-size của một unmanaged element type.
    /// Caller đã check <see cref="TypeMetadata.IsUnmanaged"/> — chỉ unmanaged type mới đến đây.
    /// Với <see cref="TypeCode.Object"/> (struct như <see cref="Guid"/>, custom struct):
    /// dùng <see cref="Unsafe.SizeOf{T}"/> qua delegate cache thay vì hardcode sai.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetElementSize(Type type)
    {
        if (type.IsEnum)
        {
            return GetElementSize(Enum.GetUnderlyingType(type));
        }

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte or TypeCode.SByte or TypeCode.Boolean => 1,
            TypeCode.Char or TypeCode.Int16 or TypeCode.UInt16 => 2,
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Single => 4,
            TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Double or TypeCode.DateTime => 8,
            TypeCode.Decimal => 16,
            TypeCode.Empty => GetUnsafeSizeOf(type),
            TypeCode.Object => GetUnsafeSizeOf(type),
            TypeCode.DBNull => GetUnsafeSizeOf(type),
            TypeCode.String => GetUnsafeSizeOf(type),
            _ => 0
        };
    }

    /// <summary>
    /// Gọi <c>Unsafe.SizeOf&lt;T&gt;()</c> với runtime <see cref="Type"/> bằng cách cache delegate
    /// sau lần đầu — tương tự pattern của <c>TypeMetadata.Core</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetUnsafeSizeOf(Type type)
    {
        return s_elementSizeCache.GetOrAdd(type, static t =>
        {
            MethodInfo method =
                typeof(Unsafe)
                    .GetMethod(nameof(Unsafe.SizeOf), BindingFlags.Public | BindingFlags.Static)!
                    .MakeGenericMethod(t);

            return method.CreateDelegate<Func<int>>();
        })();
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
        if (buffer.Length < this.Length)
        {
            throw new ArgumentException(
                $"Buffer too small: length={buffer.Length}, required>={this.Length}, type={typeof(TSelf).FullName}.");
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
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{typeof(TSelf).Name}] s_autoMagic=0x{s_autoMagic:X8} FixedSize={s_cachedFixedSize.Value?.ToString(CultureInfo.InvariantCulture) ?? "dynamic"} Properties={s_metadata.Value.Length}");

        foreach (PropertyMetadata meta in s_metadata.Value)
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
            ["FixedSize"] = s_cachedFixedSize.Value?.ToString(CultureInfo.InvariantCulture) ?? "dynamic",
            ["PropertiesCount"] = s_metadata.Value.Length,
            ["Properties"] = Array.ConvertAll(s_metadata.Value, meta => meta.ToString())
        };
    }

    /// <inheritdoc/>
    public override string ToString() => $"{typeof(TSelf).Name}(Magic=0x{this.MagicNumber:X8}, OpCode={this.OpCode}, Flags={this.Flags}, Priority={this.Priority}, Protocol={this.Protocol})";

    #endregion Diagnostics

    #region Private Methods

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<(PropertyInfo p, SerializeOrderAttribute? order)> EnumerateSerializableProperties()
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

    #endregion Private Methods
}
