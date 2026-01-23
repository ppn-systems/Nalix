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
    /// All serializable properties as pre-compiled PropertyMetadata.
    /// Lazy{T} guarantees thread-safe single initialization without explicit locking.
    /// Using System.Linq only at startup (inside the Lazy factory) — never in hot paths.
    /// </summary>
    [SerializeIgnore]
    private static readonly Lazy<PropertyMetadata[]> s_metadata = new(
        static () =>
        [
            .. Enumerable.Select(
                Enumerable.OrderBy(
                    Enumerable.Where(
                        Enumerable.Select(
                            typeof(TSelf).GetProperties(
                                BindingFlags.Public |
                                BindingFlags.Instance),
                            static p => (
                                p,
                                order: CustomAttributeExtensions
                                           .GetCustomAttribute<SerializeOrderAttribute>(p),
                                ignore: CustomAttributeExtensions
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

    /// <summary>
    /// null  → has dynamic properties, call ComputeDynamicLength() at runtime.
    /// value → all properties are fixed-size, return directly.
    /// Using ushort? avoids the "0-as-sentinel" ambiguity from the previous version.
    /// </summary>
    [SerializeIgnore]
    private static readonly Lazy<ushort?> s_cachedFixedSize = new(
        static () =>
        {
            ushort size = PacketConstants.HeaderSize;
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
            // Fast path: all properties are fixed-size → return cached value directly.
            ushort? fixedSize = s_cachedFixedSize.Value;
            return fixedSize ?? this.COMPUTE_DYNAMIC_LENGTH();
        }
    }

    /// <summary>
    /// Walks all properties to compute the actual wire-length at runtime.
    /// Fixed-size contributions use the cached <see cref="PropertyMetadata.FixedSize"/>;
    /// dynamic contributions call through to the compiled getter delegate.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private ushort COMPUTE_DYNAMIC_LENGTH()
    {
        ushort size = PacketConstants.HeaderSize;

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
                string str when str.Length > 0
                    => (ushort)(Encoding.UTF8.GetByteCount(str) + sizeof(ushort)),

                string => sizeof(ushort),

                byte[] { Length: > 0 } bytes
                    => (ushort)(bytes.Length + sizeof(int)),

                byte[] => sizeof(int),

                _ => 0
            };
        }

        return size;
    }

    #endregion Length

    #region APIs

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override byte[] Serialize() => LiteSerializer.Serialize((TSelf)this);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Serialize(Span<byte> buffer)
    {
        return buffer.Length < this.Length
            ? throw new ArgumentException($"Buffer too small for {typeof(TSelf).Name}. Required: {this.Length}, Actual: {buffer.Length}.", nameof(buffer))
            : LiteSerializer.Serialize((TSelf)this, buffer);
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
    /// Thrown when deserialization reads zero bytes (corrupt or truncated frame).
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "<Pending>")]
    public static TSelf Deserialize(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            throw new ArgumentException(
                $"Cannot deserialize {typeof(TSelf).Name} from an empty buffer.",
                nameof(buffer));
        }
        TSelf packet = new();

        int bytesRead = LiteSerializer.Deserialize(buffer, ref packet);

        return bytesRead == 0
            ? throw new InvalidOperationException(
                $"Failed to deserialize {typeof(TSelf).Name}: zero bytes were consumed. " +
                $"Buffer length: {buffer.Length}.")
            : packet;
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
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IDictionary<string, object> GenerateReportData()
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
}
