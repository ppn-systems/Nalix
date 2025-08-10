using Nalix.Common.Packets.Interfaces;
using Nalix.Shared.Extensions;

namespace Nalix.Network.Dispatch.Analyzers;

internal static class PacketRegistry
{
    // Dictionary to store deserialization delegates, keyed by MagicNumber (UInt32)
    private static readonly System.Collections.Generic.Dictionary<
        System.UInt32, System.Func<System.ReadOnlySpan<System.Byte>, IPacket>> _packetFactories;

    private static readonly System.Collections.Generic.Dictionary<
        System.UInt32, System.Func<System.ReadOnlySpan<System.Byte>, IPacket>> _cachedFactories;

    // Static constructor to initialize and register packet types
    static PacketRegistry()
    {
        _packetFactories = [];
        _cachedFactories = [];

        // Get the executing assembly
        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();

        // Get all types in the assembly
        System.Type[] types = assembly.GetTypes();

        foreach (System.Type type in types)
        {
            if (!typeof(IPacket).IsAssignableFrom(type) || !type.IsClass)
            {
                continue;
            }

            // Get the static MagicNumber field from the type
            System.Reflection.FieldInfo? magicNumberField = type.GetField("MagicNumber",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (magicNumberField == null)
            {
                continue;
            }

            System.Object? magicNumberObj = magicNumberField.GetValue(null);
            if (magicNumberObj is not System.UInt32 magicNumber)
            {
                continue;
            }

            // Try to get the transformer interface (IPacketTransformer<>)
            System.String? ipacketTransformerFullName = typeof(IPacketTransformer<>).FullName;
            if (ipacketTransformerFullName == null)
            {
                continue;
            }

            System.Type? transformerType = type.GetInterface(ipacketTransformerFullName);

            // Get the Deserialize method from the transformer
            System.Reflection.MethodInfo? deserializeMethod = transformerType?.GetMethod("Deserialize");
            if (deserializeMethod == null)
            {
                continue;
            }

            // Cache the deserialization delegate for later use
            _packetFactories[magicNumber] = CreateDeserializerDelegate(deserializeMethod);
        }
    }

    // Lazy loading: Cache deserialization delegate when it's actually needed
    private static System.Func<System.ReadOnlySpan<System.Byte>, IPacket> CreateDeserializerDelegate(
        System.Reflection.MethodInfo deserializeMethod)
    {
        return buffer =>
        {
            // Lazy load from cache if already created
            System.Type declaringType = deserializeMethod.DeclaringType ??
                throw new System.InvalidOperationException("Deserialize method's DeclaringType is null.");

            System.UInt32 key = (System.UInt32)declaringType.GetHashCode();
            if (!_cachedFactories.TryGetValue(key,
                out System.Func<System.ReadOnlySpan<System.Byte>, IPacket>? value))
            {
                System.Func<System.ReadOnlySpan<System.Byte>, IPacket> factory =
                    deserializeMethod.CreateDelegate<System.Func<System.ReadOnlySpan<System.Byte>, IPacket>>();

                value = factory;
                _cachedFactories[key] = value;
            }
            return value(buffer);
        };
    }

    // Resolve packet deserializer based on magic number
    public static System.Func<System.ReadOnlySpan<System.Byte>, IPacket>? ResolvePacketDeserializer(System.UInt32 magicNumber)
    {
        // Use magicNumber directly to retrieve the deserializer delegate
        return _cachedFactories.TryGetValue(magicNumber, out System.Func<System.ReadOnlySpan<System.Byte>, IPacket>? cachedFactory)
            ? cachedFactory
            : _packetFactories.TryGetValue(magicNumber, out System.Func<System.ReadOnlySpan<System.Byte>, IPacket>? factory)
            ? factory
            : null;
    }

    // Resolve packet deserializer based on raw bytes (uses ReadMagicNumber extension method)
    public static System.Func<System.ReadOnlySpan<System.Byte>, IPacket>? ResolvePacketDeserializer(
        System.ReadOnlySpan<System.Byte> raw)
    {
        System.UInt32 magicNumber = raw.ReadMagicNumber();
        return ResolvePacketDeserializer(magicNumber);
    }
}
