// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.DataFrames.Internal;
using Nalix.Codec.Extensions;
using Nalix.Codec.Serialization;

namespace Nalix.Codec.DataFrames;

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

    #region Constructor

    /// <summary>
    /// Assigns the automatically derived <see cref="FrameBase.Header"/>.MagicNumber
    /// so that every packet is self-identifying on the wire without any attribute.
    /// </summary>
    protected PacketBase() => this.MagicNumber = PacketTypeCache<TSelf>.AutoMagic;

    #endregion Constructor

    #region Length

    /// <inheritdoc/>
    [SkipClean]
    [SerializeIgnore]
    public override int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => PacketTypeCache<TSelf>.GetLength((TSelf)this);
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
        int staticSize = PacketTypeCache<TSelf>.StaticSize;
        if (buffer.Length < staticSize)
        {
            throw new ArgumentException(
                $"Buffer too small: length={buffer.Length}, required>={staticSize}, type={typeof(TSelf).FullName}.");
        }

        try
        {
            return LiteSerializer.Serialize((TSelf)this, buffer);
        }
        catch (Exception ex) when (
            ex is InvalidOperationException ||
            ex is InternalErrorException ||
            ex is ArgumentOutOfRangeException)
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
    /// Creates or rents an instance of <typeparamref name="TSelf"/>.
    /// </summary>
    /// <returns>A new or rented <typeparamref name="TSelf"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory pattern")]
    public static TSelf Create() => PacketRegistry.Manager == null ? new TSelf() : PacketRegistry.Manager.Get<TSelf>();

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

    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override void ResetForPool()
    {
        // Reset all user-defined serializable properties via compiled delegates.
        // No GetCustomAttribute calls in this path.
        foreach (PropertyMetadata meta in PacketTypeCache<TSelf>.Metadata)
        {
            if (meta.IsWritable)
            {
                meta.SetValue(this, meta.DefaultValue);
            }
        }

        // Explicitly reset all FrameBase header fields to well-known defaults.
        // These are declared in the base class so _metadata may or may not include them
        // depending on whether SerializeOrder is defined — reset them unconditionally.
        this.SequenceId = 0;
        this.Flags = PacketFlags.SYSTEM;
        this.Priority = PacketPriority.NONE;

        // Restore type identity — never reset to 0.
        this.MagicNumber = PacketTypeCache<TSelf>.AutoMagic;
    }

    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnRent() => Volatile.Write(ref _isRented, 1);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Design", "CA1063:Implement IDisposable Correctly", Justification = "<Pending>")]
    [SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "<Pending>")]
    public void Dispose() => this.Dispose(true);

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
            PacketRegistry.Manager?.Return((TSelf)this);
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
            $"[{typeof(TSelf).Name}] s_autoMagic=0x{PacketTypeCache<TSelf>.AutoMagic:X8} StaticSize={PacketTypeCache<TSelf>.StaticSize} " +
            $"Properties={PacketTypeCache<TSelf>.All.Length} DynamicGetters={PacketTypeCache<TSelf>.SizeGettersCount}");

        foreach (PropertyMetadata meta in PacketTypeCache<TSelf>.All)
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
            ["AutoMagic"] = $"0x{PacketTypeCache<TSelf>.AutoMagic:X8}",
            ["StaticSize"] = PacketTypeCache<TSelf>.StaticSize.ToString(CultureInfo.InvariantCulture),
            ["PropertiesCount"] = PacketTypeCache<TSelf>.All.Length,
            ["DynamicGettersCount"] = PacketTypeCache<TSelf>.SizeGettersCount,
            ["Properties"] = Array.ConvertAll(PacketTypeCache<TSelf>.All, meta => meta.ToString())
        };
    }

    /// <inheritdoc/>
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteString("TypeName", typeof(TSelf).Name);
        writer.WriteString("AutoMagic", $"0x{PacketTypeCache<TSelf>.AutoMagic:X8}");
        writer.WriteString("StaticSize", PacketTypeCache<TSelf>.StaticSize.ToString(CultureInfo.InvariantCulture));
        writer.WriteNumber("PropertiesCount", PacketTypeCache<TSelf>.All.Length);
        writer.WriteNumber("DynamicGettersCount", PacketTypeCache<TSelf>.SizeGettersCount);

        writer.WriteStartArray("Properties");
        foreach (PropertyMetadata meta in PacketTypeCache<TSelf>.All)
        {
            writer.WriteStringValue(meta.ToString());
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"{typeof(TSelf).Name}(Magic=0x{this.Header.MagicNumber:X8}, OpCode={this.Header.OpCode}, Flags={this.Header.Flags}, Priority={this.Header.Priority}, SequenceId={this.Header.SequenceId})";

    #endregion Diagnostics

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void VALIDATE_BUFFER_HEADER(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            throw new ArgumentException($"Cannot deserialize {typeof(TSelf).Name}: buffer is empty.");
        }

        if (buffer.Length < PacketTypeCache<TSelf>.StaticSize)
        {
            throw new SerializationFailureException(
                $"Insufficient buffer for {typeof(TSelf).Name}: length={buffer.Length}, required={PacketTypeCache<TSelf>.StaticSize}.");
        }

        ref readonly PacketHeader header = ref buffer.AsHeaderRef();
        if (header.MagicNumber != PacketTypeCache<TSelf>.AutoMagic)
        {
            throw new SerializationFailureException(
                $"Magic number mismatch: type={typeof(TSelf).Name}, buffer=0x{header.MagicNumber:X8}, expected=0x{PacketTypeCache<TSelf>.AutoMagic:X8}. " +
                "The received packet type does not match the target deserialization type.");
        }
    }

    #endregion Private Methods
}
