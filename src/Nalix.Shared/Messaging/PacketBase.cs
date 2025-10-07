// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Infrastructure.Caching;
using Nalix.Common.Messaging.Packets;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Common.Messaging.Protocols;
using Nalix.Common.Serialization;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Serialization;
using System.Linq;

namespace Nalix.Shared.Messaging;

/// <summary>
/// Base class for all packets with automatic serialization and pooling.
/// Eliminates boilerplate code for Length, Serialize, Deserialize, and ResetForPool.
/// </summary>
public abstract class PacketBase<TSelf> : FrameBase, IPoolable, IPacketDeserializer<TSelf> where TSelf : PacketBase<TSelf>, new()
{
    /// <inheritdoc/>
    [SerializeIgnore]
    public override System.UInt16 Length
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get
        {
            var fixedSize = _cachedFixedSize.Value;
            if (fixedSize > 0)
            {
                return fixedSize;
            }

            // Has dynamic size properties - calculate at runtime
            System.UInt16 size = PacketConstants.HeaderSize;
            foreach (System.Reflection.PropertyInfo prop in _serializableProperties.Value)
            {
                SerializeDynamicSizeAttribute? dynamicAttr = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<SerializeDynamicSizeAttribute>(prop);
                if (dynamicAttr != null)
                {
                    System.Object? value = prop.GetValue(this);
                    if (value is System.Byte[] bytes)
                    {
                        size += (System.UInt16)bytes.Length;
                    }
                    else if (value is System.String str)
                    {
                        size += (System.UInt16)(str?.Length ?? 0);
                    }
                }
                else
                {
                    size += GET_PROPERTY_SIZE(prop.PropertyType);
                }
            }
            return size;
        }
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.Byte[] Serialize() => LiteSerializer.Serialize(this);

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.Int32 Serialize(System.Span<System.Byte> buffer) => LiteSerializer.Serialize(this, buffer);

    /// <summary>
    /// Deserializes a packet from buffer using object pooling.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TSelf Deserialize(System.ReadOnlySpan<System.Byte> buffer)
    {
        TSelf packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                               .Get<TSelf>();

        System.Int32 bytesRead = LiteSerializer.Deserialize(buffer, ref packet);
        if (bytesRead == 0)
        {
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return(packet);

            throw new System.InvalidOperationException(
                $"Failed to deserialize {typeof(TSelf).Name}: No bytes were read.");
        }

        return packet;
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        // Auto-reset all serializable properties to default values
        foreach (System.Reflection.PropertyInfo prop in _serializableProperties.Value)
        {
            if (!prop.CanWrite)
            {
                continue;
            }

            System.Object? defaultValue = GET_DEFAULT_VALUE(prop.PropertyType);
            prop.SetValue(this, defaultValue);
        }

        // Reset base packet fields
        this.Flags = PacketFlags.NONE;
        this.Protocol = ProtocolType.NONE;
    }

    #region Fields

    private static readonly System.Lazy<System.Reflection.PropertyInfo[]> _serializableProperties = new(() =>
    {
        return [.. typeof(TSelf)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p =>
                System.Reflection.CustomAttributeExtensions.GetCustomAttribute<SerializeOrderAttribute>(p) != null &&
                System.Reflection.CustomAttributeExtensions.GetCustomAttribute<SerializeIgnoreAttribute>(p) == null)
            .OrderBy(p => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<SerializeOrderAttribute>(p)!.Order)];
    });

    private static readonly System.Lazy<System.UInt16> _cachedFixedSize = new(() =>
    {
        System.UInt16 size = PacketConstants.HeaderSize;

        foreach (System.Reflection.PropertyInfo prop in _serializableProperties.Value)
        {
            SerializeDynamicSizeAttribute? dynamicAttr = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<SerializeDynamicSizeAttribute>(prop);
            if (dynamicAttr != null)
            {
                // Dynamic size property - cannot pre-calculate
                return 0; // Signal that we need runtime calculation
            }

            size += GET_PROPERTY_SIZE(prop.PropertyType);
        }

        return size;
    });

    #endregion Fields

    #region Private Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt16 GET_PROPERTY_SIZE(System.Type type)
    {
        return System.Type.GetTypeCode(type) switch
        {
            System.TypeCode.Byte => 1,
            System.TypeCode.Boolean => 1,
            System.TypeCode.Int16 => 2,
            System.TypeCode.UInt16 => 2,
            System.TypeCode.Int32 => 4,
            System.TypeCode.UInt32 => 4,
            System.TypeCode.Int64 => 8,
            System.TypeCode.UInt64 => 8,
            System.TypeCode.Single => 4,
            System.TypeCode.Double => 8,
            System.TypeCode.Decimal => 16,
            _ => type.IsEnum ? GET_PROPERTY_SIZE(System.Enum.GetUnderlyingType(type)) : (System.UInt16)0
        };
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Object? GET_DEFAULT_VALUE(System.Type type)
    {
        return type == typeof(System.Byte[])
            ? System.Array.Empty<System.Byte>() : type == typeof(System.String)
            ? System.String.Empty : type.IsValueType ? System.Activator.CreateInstance(type) : null;
    }

    #endregion Private Methods
}