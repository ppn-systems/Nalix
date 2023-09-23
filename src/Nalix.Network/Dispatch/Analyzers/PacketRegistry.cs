using Nalix.Common.Packets.Interfaces;
using Nalix.Shared.Extensions;

namespace Nalix.Network.Dispatch.Analyzers;

internal static class PacketRegistry
{
    // Dictionary to store deserialization delegates, keyed by MagicNumber
    private static readonly System.Collections.Generic.Dictionary<
        System.UInt32, System.Func<System.ReadOnlySpan<System.Byte>, IPacket>> _packetFactories = [];

    // Cache for storing delegate creation for deserialization (to avoid reflection every time)
    private static readonly System.Collections.Generic.Dictionary<
        System.Type, System.Func<System.ReadOnlySpan<System.Byte>, IPacket>> _deserializeDelegates = [];

    // Static constructor to initialize and register packet types
    static PacketRegistry()
    {
        // Get the executing assembly
        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();

        // Get all types in the assembly
        System.Type[] types = assembly.GetTypes();

        foreach (System.Type type in types)
        {
            // Ensure the type implements IPacket and is a class
            if (!typeof(IPacket).IsAssignableFrom(type) || !type.IsClass)
            {
                continue;
            }

            // Get the static MagicNumber field from the type
            System.Reflection.FieldInfo? magicNumberField = type.GetField(
                "MagicNumber",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static);
            if (magicNumberField == null)
            {
                continue;
            }

            // Get MagicNumber value
            System.Object? magicNumberObj = magicNumberField.GetValue(null);
            if (magicNumberObj is not System.UInt32 magicNumber)
            {
                continue;
            }

            // Try to get the transformer interface (IPacketTransformer<>)
            System.Type ipacketTransformerType = typeof(IPacketTransformer<>);
            System.String? ipacketTransformerTypeName = ipacketTransformerType.FullName;
            if (ipacketTransformerTypeName == null)
            {
                continue;
            }
            System.Type? transformerType = type.GetInterface(ipacketTransformerTypeName);

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

    // Caching the deserialization delegate to minimize reflection overhead
    private static System.Func<System.ReadOnlySpan<System.Byte>, IPacket> CreateDeserializerDelegate(
        System.Reflection.MethodInfo deserializeMethod)
    {
        System.Type declaringType = deserializeMethod.DeclaringType
            ?? throw new System.InvalidOperationException("Deserialize method's DeclaringType is null.");

        // Check if the delegate has already been cached
        if (_deserializeDelegates.TryGetValue(
            declaringType, out System.Func<System.ReadOnlySpan<System.Byte>, IPacket>? value))
        {
            return value;
        }

        // Create a delegate to invoke the Deserialize method
        System.Func<System.ReadOnlySpan<System.Byte>, IPacket> deserializerDelegate = new((buffer) =>
        {
            System.Object? result = deserializeMethod.Invoke(null, [buffer.ToArray()]);
            return result is IPacket packet
            ? packet
            : throw new System.InvalidOperationException("Deserialize returned null or non-IPacket.");
        });

        // Cache the delegate for later use
        _deserializeDelegates[declaringType] = deserializerDelegate;

        return deserializerDelegate;
    }

    // Resolve packet deserializer based on magic number
    public static System.Func<System.ReadOnlySpan<System.Byte>, IPacket>?
        ResolvePacketDeserializer(System.UInt32 magicNumber)
        => _packetFactories.TryGetValue(
            magicNumber, out System.Func<System.ReadOnlySpan<System.Byte>, IPacket>? factory) ? factory : null;

    // Resolve packet deserializer based on raw bytes (uses ReadMagicNumber extension method)
    public static System.Func<System.ReadOnlySpan<System.Byte>, IPacket>?
        ResolvePacketDeserializer(System.ReadOnlySpan<System.Byte> raw)
        => ResolvePacketDeserializer(raw.ReadMagicNumber());
}
