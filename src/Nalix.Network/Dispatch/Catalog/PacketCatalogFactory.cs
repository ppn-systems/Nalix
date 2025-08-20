// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Models;
using Nalix.Common.Security.Enums;
using Nalix.Network.Dispatch.Internal;
using Nalix.Shared.Injection;
using Nalix.Shared.Messaging.Binary;
using Nalix.Shared.Messaging.Control;
using Nalix.Shared.Messaging.Text;

namespace Nalix.Network.Dispatch.Catalog;

/// <summary>
/// Provides a fluent builder to compose a <see cref="PacketCatalog"/> from
/// explicit packet types and/or assemblies scanned via reflection.
/// </summary>
/// <remarks>
/// <para>
/// The builder discovers packet types that implement <see cref="IPacket"/> and
/// are decorated with <see cref="MagicNumberAttribute"/>. For each discovered type,
/// it expects a matching <c>IPacketTransformer&lt;TPacket&gt;</c> implementation that
/// exposes static methods for <c>Deserialize</c>, <c>Compress</c>, <c>Decompress</c>,
/// <c>Encrypt</c>, and <c>Decrypt</c>.
/// </para>
/// <para>
/// The resulting catalog is immutable and safe for concurrent read access.
/// Ensure transformer methods are preserved under trimming/AOT if applicable.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var builder = new PacketCatalogFactory()
///     .IncludeAssembly(typeof(MyPacket).Assembly)
///     .RegisterPacket&lt;HealthCheckPacket&gt;();
///
/// PacketCatalog catalog = builder.CreateCatalog();
///
/// if (catalog.TryGetDeserializer(magic, out var deser) &amp;&amp;
///     catalog.TryGetTransformer(typeof(MyPacket), out var xform))
/// {
///     if (catalog.TryDeserialize(raw, out var pkt))
///     {
///         // Encrypt with AES-256
///         var encrypted = xform.Encrypt(pkt, keyBytes, SymmetricAlgorithmType.Aes256Cbc);
///     }
/// }
/// </code>
/// </example>
public sealed class PacketCatalogFactory
{
    private static readonly System.Collections.Generic.HashSet<System.String> Namespaces = new(
        System.Linq.Enumerable.Where(
        [
            typeof(Text256).Namespace,
            typeof(Control).Namespace,
            typeof(Binary128).Namespace
        ], ns => ns is not null)!,
        System.StringComparer.Ordinal
    );

    private readonly System.Collections.Generic.HashSet<System.Type> _explicitPacketTypes = [];
    private readonly System.Collections.Generic.HashSet<System.Reflection.Assembly> _assemblies = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketCatalogFactory"/> class.
    /// Registers the <see cref="Binary128"/> packet type by default.
    /// </summary>
    public PacketCatalogFactory()
    {
        _ = this.RegisterPacket<Binary128>()
                .RegisterPacket<Binary256>()
                .RegisterPacket<Binary512>()
                .RegisterPacket<Binary1024>();

        _ = this.RegisterPacket<Text256>()
                .RegisterPacket<Text512>()
                .RegisterPacket<Text1024>();

        _ = this.RegisterPacket<Control>()
                .RegisterPacket<Handshake>();
    }

    /// <summary>
    /// Adds an assembly to the discovery set for packet types.
    /// </summary>
    /// <param name="asm">
    /// The assembly to scan. If <see langword="null"/>, the call is ignored.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketCatalogFactory"/> instance for method chaining.
    /// </returns>
    public PacketCatalogFactory IncludeAssembly(System.Reflection.Assembly asm)
    {
        if (asm is null)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(PacketCatalogFactory)}] IncludeAssembly ignored: assembly is null.");
            return this;
        }

        if (_assemblies.Add(asm))
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(PacketCatalogFactory)}] Assembly registered for scan: {asm.FullName}");
        }
        else
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(PacketCatalogFactory)}] Assembly already registered, skipping: {asm.FullName}");
        }
        return this;
    }

    /// <summary>
    /// Adds an explicit packet type and skips scanning for it.
    /// </summary>
    /// <typeparam name="TPacket">The packet type implementing <see cref="IPacket"/>.</typeparam>
    /// <returns>
    /// The current <see cref="PacketCatalogFactory"/> instance for method chaining.
    /// </returns>
    public PacketCatalogFactory RegisterPacket<TPacket>() where TPacket : IPacket
    {
        if (_explicitPacketTypes.Add(typeof(TPacket)))
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(PacketCatalogFactory)}] Explicit packet type registered: {typeof(TPacket).FullName}");
        }
        else
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(PacketCatalogFactory)}] Explicit packet type already " +
                                           $"registered, skipping: {typeof(TPacket).FullName}");
        }
        return this;
    }

    /// <summary>
    /// Builds a frozen <see cref="PacketCatalog"/> by scanning configured assemblies
    /// and explicit packet types, binding deserializers and transformers.
    /// </summary>
    /// <remarks>
    /// Uses reflection to locate static transformer methods. Ensure these are preserved
    /// when trimming or compiling AOT.
    /// </remarks>
    /// <returns>
    /// An immutable <see cref="PacketCatalog"/> with deserializer and transformer lookups.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if multiple packet types declare the same <see cref="MagicNumberAttribute.MagicNumber"/>.
    /// </exception>
    public PacketCatalog CreateCatalog()
    {
        System.Collections.Generic.Dictionary<System.Type, PacketTransformer> transformers = [];
        System.Collections.Generic.Dictionary<System.UInt32, PacketDeserializer> deserializers = [];

        // 1) Aggregate candidate packet types
        System.Collections.Generic.HashSet<System.Type> candidates = [.. _explicitPacketTypes];

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[{nameof(PacketCatalogFactory)}] " +
                                      $"assemblies={_assemblies.Count}, explicitTypes={_explicitPacketTypes.Count}");

        foreach (System.Reflection.Assembly asm in _assemblies)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(PacketCatalogFactory)}] Scanning: {asm.FullName}");

            foreach (System.Type type in ReflectionHelpers.SafeGetTypes(asm))
            {
                if (type is null || !type.IsClass || type.IsAbstract)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[{nameof(PacketCatalogFactory)}] Skipped type (not concrete class): {type?.FullName}");
                    continue;
                }

                if (type.Namespace is not null && Namespaces.Contains(type.Namespace))
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[{nameof(PacketCatalogFactory)}] Skipped default packet type: {type.FullName}");
                    continue;
                }

                if (!typeof(IPacket).IsAssignableFrom(type))
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[{nameof(PacketCatalogFactory)}] Skipped type (not IPacket): {type?.FullName}");
                    continue;
                }

                _ = candidates.Add(type);
            }
        }

        if (candidates.Count == 0)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[{nameof(PacketCatalogFactory)}] No candidate packet types were discovered. " +
                                          $"The resulting catalog will be empty.");
        }

        // 2) CreateCatalog maps
        foreach (System.Type type in candidates)
        {
            MagicNumberAttribute? magicAttr = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<MagicNumberAttribute>(type);
            if (magicAttr is null)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[{nameof(PacketCatalogFactory)}] " +
                                               $"Type has no MagicNumberAttribute, skipping: {type.FullName}");
                continue; // only types with magic number are packets
            }

            // Find IPacketTransformer<TPacket>
            System.Type? transformerIface = System.Linq.Enumerable.FirstOrDefault(type.GetInterfaces(),
                i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPacketTransformer<>));

            if (transformerIface is null)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[{nameof(PacketCatalogFactory)}] Packet type has MagicNumber but no IPacketTransformer<>: " +
                                               $"{type.FullName}, magic=0x{magicAttr.MagicNumber:X8}");
                continue;
            }

            System.Type closed = typeof(IPacketTransformer<>).MakeGenericType(type);

            // CreateCatalog deserializer
            System.Reflection.MethodInfo? deserialize = closed.PublicStatic(nameof(IPacketTransformer<IPacket>.Deserialize));
            if (deserialize != null)
            {
                if (deserializers.ContainsKey(magicAttr.MagicNumber))
                {
                    if (InstanceManager.Instance.GetExistingInstance<ILogger>() != null)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()!
                                                .Error($"[{nameof(PacketCatalogFactory)}] Duplicate MagicNumber found: " +
                                                       $"0x{magicAttr.MagicNumber:X8} on {type.FullName}");

                        continue; // Skip this type, already registered
                    }
                    else
                    {
                        throw new System.InvalidOperationException(
                            $"[{nameof(PacketCatalogFactory)}] " +
                            $"Duplicate MagicNumber 0x{magicAttr.MagicNumber:X8} on {type.FullName}");
                    }
                }

                PacketDeserializer del = deserialize.CreateDelegate<PacketDeserializer>();
                deserializers[magicAttr.MagicNumber] = del;
            }

            // CreateCatalog transformer
            System.Reflection.MethodInfo? encrypt = closed.PublicStatic(nameof(IPacketTransformer<IPacket>.Encrypt));
            System.Reflection.MethodInfo? decrypt = closed.PublicStatic(nameof(IPacketTransformer<IPacket>.Decrypt));
            System.Reflection.MethodInfo? compress = closed.PublicStatic(nameof(IPacketTransformer<IPacket>.Compress));
            System.Reflection.MethodInfo? decompress = closed.PublicStatic(nameof(IPacketTransformer<IPacket>.Decompress));

            if (compress == null)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[{nameof(PacketCatalogFactory)}] Missing transformer methods for {type.FullName} " +
                                              $"(Compress), skipping transformers.");
                continue;
            }

            if (decompress == null)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[{nameof(PacketCatalogFactory)}] Missing transformer methods for {type.FullName} " +
                                              $"(Decompress), skipping transformers.");
                continue;
            }

            if (encrypt == null)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[{nameof(PacketCatalogFactory)}] Missing transformer methods for {type.FullName} " +
                                              $"(Encrypt), skipping transformers.");
                continue;
            }

            if (decrypt == null)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[{nameof(PacketCatalogFactory)}] Missing transformer methods for {type.FullName} " +
                                              $"(Decrypt), skipping transformers.");
                continue;
            }

            transformers[type] = new PacketTransformer(
                compress.CreateDelegate<System.Func<IPacket, IPacket>>(),
                decompress.CreateDelegate<System.Func<IPacket, IPacket>>(),
                encrypt.CreateDelegate<System.Func<IPacket, System.Byte[], SymmetricAlgorithmType, IPacket>>(),
                decrypt.CreateDelegate<System.Func<IPacket, System.Byte[], SymmetricAlgorithmType, IPacket>>());
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[{nameof(PacketCatalogFactory)}] " +
                                      $"Built: {deserializers.Count} packets, {transformers.Count} transformers.");

        return new PacketCatalog(
            System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(transformers),
            System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(deserializers)
        );
    }
}
