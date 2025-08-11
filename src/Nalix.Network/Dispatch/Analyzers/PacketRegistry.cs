using Nalix.Common.Attributes;
using Nalix.Common.Packets.Interfaces;
using Nalix.Shared.Extensions;
using System.Linq;
using System.Reflection;

namespace Nalix.Network.Dispatch.Analyzers;

internal static class PacketRegistry
{
    private static readonly System.Collections.Generic.Dictionary<
        System.UInt32, System.Func<System.ReadOnlySpan<System.Byte>, IPacket>> _packetFactories;

    private static readonly System.Collections.Generic.Dictionary<
        System.UInt32, System.Func<System.ReadOnlySpan<System.Byte>, IPacket>> _cachedFactories;

    static PacketRegistry()
    {
        _packetFactories = [];
        _cachedFactories = [];
        System.Collections.Generic.List<Assembly> assembliesToScan = [];

        if (Assembly.GetEntryAssembly() is { } entryAsm)
        {
            assembliesToScan.Add(entryAsm);
        }

        assembliesToScan.AddRange(
            System.AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic &&
                            !System.String.IsNullOrWhiteSpace(a.FullName) &&
                            a.GetTypes().Any(t => t.Namespace != null &&
                                                  t.Namespace.StartsWith(
                                                      $"{nameof(Nalix)}.{nameof(Shared)}.{nameof(Shared.Messaging)}")))
        );

        Initialize([.. assembliesToScan.Distinct()]);
    }

    /// <summary>
    /// Initializes the registry by scanning the given assemblies for packet types.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan for packets.</param>
    public static void Initialize(params Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = [Assembly.GetExecutingAssembly()];
        }

        foreach (Assembly assembly in assemblies.Distinct())
        {
            RegisterPacketsFromAssembly(assembly);
        }
    }

    private static void RegisterPacketsFromAssembly(Assembly assembly)
    {
        foreach (System.Type type in assembly.GetTypes())
        {
            if (!typeof(IPacket).IsAssignableFrom(type) || !type.IsClass)
            {
                continue;
            }

            // Check if the type has the MagicNumberAttribute
            if (type.GetCustomAttribute<MagicNumberAttribute>() is not MagicNumberAttribute magicNumberAttribute)
            {
                continue;
            }

            System.UInt32 magicNumber = magicNumberAttribute.MagicNumber;

            // If MagicNumber already exists, throw an exception
            if (_packetFactories.ContainsKey(magicNumber))
            {
                throw new System.InvalidOperationException(
                    $"MagicNumber 0x{magicNumber:X8} is already assigned to another packet type. " +
                    $"Duplicate MagicNumber found in type {type.FullName}.");
            }

            // Try to get the transformer interface (IPacketTransformer<>)
            var ipacketTransformerFullName = typeof(IPacketTransformer<>).FullName;
            if (ipacketTransformerFullName == null)
            {
                continue;
            }

            var transformerType = type.GetInterface(ipacketTransformerFullName);
            var deserializeMethod = transformerType?.GetMethod("Deserialize");

            if (deserializeMethod == null)
            {
                continue;
            }

            // Cache the deserialization delegate for later use
            _packetFactories[magicNumber] = CreateDeserializerDelegate(magicNumber, deserializeMethod);
        }
    }

    // Lazy loading: Cache deserialization delegate when it's actually needed
    private static System.Func<System.ReadOnlySpan<System.Byte>, IPacket> CreateDeserializerDelegate(
        System.UInt32 magicNumber, MethodInfo deserializeMethod)
    {
        return buffer =>
        {
            if (!_cachedFactories.TryGetValue(magicNumber,
                out System.Func<System.ReadOnlySpan<System.Byte>, IPacket>? factory))
            {
                factory = deserializeMethod.CreateDelegate<System.Func<System.ReadOnlySpan<System.Byte>, IPacket>>();
                _cachedFactories[magicNumber] = factory;
            }
            return factory(buffer);
        };
    }

    /// <summary>
    /// Resolve packet deserializer based on magic number.
    /// </summary>
    public static System.Func<System.ReadOnlySpan<System.Byte>, IPacket>?
        ResolvePacketDeserializer(System.UInt32 magicNumber)
        => _cachedFactories.TryGetValue(magicNumber,
            out System.Func<System.ReadOnlySpan<System.Byte>, IPacket>? cachedFactory)
            ? cachedFactory : _packetFactories.TryGetValue(magicNumber,
            out System.Func<System.ReadOnlySpan<System.Byte>, IPacket>? factory) ? factory : null;

    /// <summary>
    /// Resolve packet deserializer based on raw bytes.
    /// </summary>
    public static System.Func<System.ReadOnlySpan<System.Byte>, IPacket>?
        ResolvePacketDeserializer(System.ReadOnlySpan<System.Byte> raw)
        => ResolvePacketDeserializer(raw.ReadMagicNumberLE());
}
