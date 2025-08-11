using Nalix.Common.Attributes;
using Nalix.Common.Packets.Interfaces;
using Nalix.Common.Security.Cryptography;
using Nalix.Shared.Extensions;
using System.Linq;
using System.Reflection;

namespace Nalix.Network.Dispatch.Analyzers;

internal static class PacketRegistry
{
    private static readonly System.Collections.Generic.Dictionary<
        System.UInt32, System.Func<System.ReadOnlySpan<System.Byte>, IPacket>> _packetFactories;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.UInt32, System.Func<System.ReadOnlySpan<System.Byte>, IPacket>> _cachedFactories;

    private static readonly System.Collections.Generic.Dictionary<System.Type, PacketTransformerDelegates> _transformers;

    internal record PacketTransformerDelegates(
        System.Func<IPacket, IPacket> Compress,
        System.Func<IPacket, IPacket> Decompress,
        System.Func<IPacket, System.Byte[], SymmetricAlgorithmType, IPacket> Encrypt,
        System.Func<IPacket, System.Byte[], SymmetricAlgorithmType, IPacket> Decrypt
    );

    static PacketRegistry()
    {
        _transformers = [];
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
                                                  t.Namespace.StartsWith(typeof(Shared.Messaging.BinaryPacket).Namespace!)))
        );

        Initialize([.. assembliesToScan.Distinct()]);
    }

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

            if (type.GetCustomAttribute<MagicNumberAttribute>() is not MagicNumberAttribute magicNumberAttribute)
            {
                continue;
            }

            System.UInt32 magicNumber = magicNumberAttribute.MagicNumber;

            if (_packetFactories.ContainsKey(magicNumber))
            {
                throw new System.InvalidOperationException(
                    $"MagicNumber 0x{magicNumber:X8} already assigned to another packet type. " +
                    $"Duplicate found in {type.FullName}.");
            }

            var transformerInterface = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                                     i.GetGenericTypeDefinition() == typeof(IPacketTransformer<>));

            if (transformerInterface != null)
            {
                System.Type iface = typeof(IPacketTransformer<>).MakeGenericType(type);

                // Get MethodInfo for deserialization and transformation methods
                MethodInfo? deserialize = iface.GetMethod(
                    nameof(IPacketTransformer<IPacket>.Deserialize),
                    BindingFlags.Public | BindingFlags.Static
                );
                MethodInfo? compress = iface.GetMethod(
                    nameof(IPacketTransformer<IPacket>.Compress),
                    BindingFlags.Public | BindingFlags.Static
                );
                MethodInfo? decompress = iface.GetMethod(
                    nameof(IPacketTransformer<IPacket>.Decompress),
                    BindingFlags.Public | BindingFlags.Static
                );
                MethodInfo? encrypt = iface.GetMethod(
                    nameof(IPacketTransformer<IPacket>.Encrypt),
                    BindingFlags.Public | BindingFlags.Static
                );
                MethodInfo? decrypt = iface.GetMethod(
                    nameof(IPacketTransformer<IPacket>.Decrypt),
                    BindingFlags.Public | BindingFlags.Static
                );

                if (deserialize != null)
                {
                    _packetFactories[magicNumber] = CreateDeserializerDelegate(magicNumber, deserialize);
                }

                if (compress == null || decompress == null || encrypt == null || decrypt == null)
                {
                    continue;
                }

                System.Func<IPacket, IPacket> compressDel = compress.CreateDelegate<System.Func<IPacket, IPacket>>();
                System.Func<IPacket, IPacket> decompressDel = decompress.CreateDelegate<System.Func<IPacket, IPacket>>();

                System.Func<IPacket, System.Byte[], SymmetricAlgorithmType, IPacket> encryptDel = encrypt.CreateDelegate<
                    System.Func<IPacket, System.Byte[], SymmetricAlgorithmType, IPacket>>();

                System.Func<IPacket, System.Byte[], SymmetricAlgorithmType, IPacket> decryptDel = decrypt.CreateDelegate<
                    System.Func<IPacket, System.Byte[], SymmetricAlgorithmType, IPacket>>();

                _transformers[type] = new PacketTransformerDelegates(
                    Compress: compressDel,
                    Decompress: decompressDel,
                    Encrypt: encryptDel,
                    Decrypt: decryptDel
                );
            }
        }
    }

    private static System.Func<System.ReadOnlySpan<System.Byte>, IPacket> CreateDeserializerDelegate(
        System.UInt32 magicNumber, MethodInfo deserializeMethod)
    {
        return buffer =>
        {
            System.Func<System.ReadOnlySpan<System.Byte>, IPacket> factory = _cachedFactories.GetOrAdd(magicNumber,
                _ => deserializeMethod.CreateDelegate<System.Func<System.ReadOnlySpan<System.Byte>, IPacket>>());
            return factory(buffer);
        };
    }

    // Public API
    public static System.Func<System.ReadOnlySpan<System.Byte>, IPacket>?
        ResolvePacketDeserializer(System.UInt32 magicNumber)
        => _cachedFactories.TryGetValue(magicNumber, out System.Func<System.ReadOnlySpan<System.Byte>, IPacket>? cached)
            ? cached : _packetFactories.TryGetValue(magicNumber,
                out System.Func<System.ReadOnlySpan<System.Byte>, IPacket>? factory) ? factory : null;

    public static System.Func<System.ReadOnlySpan<System.Byte>, IPacket>?
        ResolvePacketDeserializer(System.ReadOnlySpan<System.Byte> raw)
        => ResolvePacketDeserializer(raw.ReadMagicNumberLE());

    public static System.Boolean TryResolveTransformer(System.Type packetType, out PacketTransformerDelegates? transformer)
        => _transformers.TryGetValue(packetType, out transformer);
}
