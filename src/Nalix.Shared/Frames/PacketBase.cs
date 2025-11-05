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
using System.Linq;

namespace Nalix.Shared.Frames;

/// <summary>
/// Base class for all packets with automatic serialization and pooling.
/// Eliminates boilerplate code for Length, Serialize, Deserialize, and ResetForPool.
/// <para>
/// <b>MagicNumber</b> is derived automatically from <typeparamref name="TSelf"/>'s
/// full type name via FNV-1a hash — no <c>[MagicNumber]</c> attribute needed.
/// </para>
/// </summary>
public abstract class PacketBase<TSelf> : FrameBase, IPoolable, IPacketDeserializer<TSelf> where TSelf : PacketBase<TSelf>, new()
{
    #region Static Cache

    // Computed once per concrete type at class-load time.
    private static readonly System.UInt32 AutoMagic = PacketRegistryFactory.Compute(typeof(TSelf));

    // All serializable properties as pre-compiled PropertyMetadata — no further
    // reflection attribute scanning needed in hot paths.
    private static readonly System.Lazy<PropertyMetadata[]> _metadata = new(() =>
        [.. typeof(TSelf)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => (p, attr: System.Reflection.CustomAttributeExtensions.GetCustomAttribute<SerializeOrderAttribute>(p)))
            .Where(x => x.attr is not null &&
                        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<SerializeIgnoreAttribute>(x.p) is null)
            .OrderBy(x => x.attr!.Order)
            .Select(x => new PropertyMetadata(x.p))
        ]
    );

    // Zero means "has dynamic properties — compute at runtime".
    private static readonly System.Lazy<System.UInt16> _cachedFixedSize = new(() =>
    {
        System.UInt16 size = PacketConstants.HeaderSize;
        foreach (PropertyMetadata meta in _metadata.Value)
        {
            if (meta.IsDynamic)
            {
                return 0; // signal: runtime calculation required
            }

            size += meta.FixedSize;
        }
        return size;
    });

    #endregion Static Cache

    #region Constructor

    /// <summary>
    /// Assigns the automatically derived <see cref="FrameBase.MagicNumber"/>
    /// so that every packet is self-identifying on the wire without any attribute.
    /// </summary>
    protected PacketBase() => this.MagicNumber = AutoMagic;

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
            System.UInt16 fixedSize = _cachedFixedSize.Value;
            // Fast path: all properties have known fixed sizes.
            return fixedSize > 0 ? fixedSize : ComputeDynamicLength();
        }
    }

    /// <summary>
    /// Walks only the dynamic properties to compute the runtime wire-length.
    /// Fixed-size contributions are taken from <see cref="PropertyMetadata.FixedSize"/>
    /// — no attribute scanning on every call.
    /// </summary>
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

            // Dynamic: measure actual runtime content.
            size += meta.GetValue(this) switch
            {
                System.Byte[] bytes => (System.UInt16)bytes.Length,

                // Use UTF-8 byte-count, NOT char-count, to get the true wire size.
                System.String str => (System.UInt16)(System.Text.Encoding.UTF8.GetByteCount(str) + 2),

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
        return buffer.Length < this.Length
            ? throw new System.ArgumentException(
                $"Buffer too small. Required: {this.Length}, Actual: {buffer.Length}.",
                nameof(buffer))
            : LiteSerializer.Serialize<TSelf>((TSelf)this, buffer);
    }

    /// <summary>
    /// Deserializes a <typeparamref name="TSelf"/> packet from <paramref name="buffer"/>
    /// using object pooling to avoid heap allocation.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the deserializer reads zero bytes (corrupt or empty frame).
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TSelf Deserialize(System.ReadOnlySpan<System.Byte> buffer)
    {
        TSelf packet = InstanceManager.Instance
                                      .GetOrCreateInstance<ObjectPoolManager>()
                                      .Get<TSelf>();

        System.Int32 bytesRead = LiteSerializer.Deserialize(buffer, ref packet);
        if (bytesRead == 0)
        {
            InstanceManager.Instance
                           .GetOrCreateInstance<ObjectPoolManager>()
                           .Return(packet);

            throw new System.InvalidOperationException(
                $"Failed to deserialize {typeof(TSelf).Name}: No bytes were read.");
        }

        return packet;
    }

    /// <summary>
    /// Encrypts the provided packet using the specified symmetric key and cipher suite.
    /// </summary>
    /// <param name="packet">The packet to encrypt. Must not be <c>null</c>.</param>
    /// <param name="key">The symmetric key bytes used for encryption. Must not be <c>null</c> or empty.</param>
    /// <param name="algorithm">The cipher suite to use for encryption.</param>
    /// <returns>
    /// A new instance of <typeparamref name="TSelf"/> representing the encrypted packet
    /// (the returned instance may be the same object mutated by the underlying encryptor).
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="packet"/> or <paramref name="key"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="key"/> is empty or has an invalid length for the chosen algorithm.
    /// </exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Thrown when a cryptographic operation fails.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TSelf Encrypt(TSelf packet, System.Byte[] key, CipherSuiteType algorithm) => EnvelopeEncryptor.Encrypt<TSelf>(packet, key, algorithm);

    /// <summary>
    /// Decrypts the provided packet using the specified symmetric key.
    /// </summary>
    /// <param name="packet">The packet to decrypt. Must not be <c>null</c>.</param>
    /// <param name="key">The symmetric key bytes used for decryption. Must not be <c>null</c> or empty.</param>
    /// <returns>
    /// A new instance of <typeparamref name="TSelf"/> representing the decrypted packet
    /// (the returned instance may be the same object mutated by the underlying decryptor).
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="packet"/> or <paramref name="key"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="key"/> is empty or has an invalid length.
    /// </exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Thrown when a cryptographic operation fails or the payload is tampered.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TSelf Decrypt(TSelf packet, System.Byte[] key) => EnvelopeEncryptor.Decrypt<TSelf>(packet, key);

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        // Reset all serializable properties to their pre-computed defaults.
        // No GetCustomAttribute calls — everything is in PropertyMetadata.
        foreach (PropertyMetadata meta in _metadata.Value)
        {
            if (meta.IsWritable)
            {
                meta.SetValue(this, meta.DefaultValue);
            }
        }

        // Reset fixed header fields.
        this.OpCode = 0;
        this.Flags = PacketFlags.NONE;
        this.Protocol = ProtocolType.NONE;
        this.Priority = PacketPriority.NONE;
        this.MagicNumber = AutoMagic; // restore identity — never reset to 0
    }

    #endregion APIs
}