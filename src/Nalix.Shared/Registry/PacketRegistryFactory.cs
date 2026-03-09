// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Middleware.Attributes;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Packets.Transformation;
using Nalix.Common.Security.Enums;
using Nalix.Framework.Injection;
using Nalix.Shared.Frames.Controls;
using Nalix.Shared.Frames.Text;

namespace Nalix.Shared.Registry;

/// <summary>
/// Builds an immutable <see cref="PacketRegistry"/> by scanning packet types and
/// binding their static transformation functions with maximum performance.
/// </summary>
/// <remarks>
/// <para>
/// This implementation binds packet transformation methods using <b>unsafe function
/// pointers</b> (i.e., <c>delegate*</c>) to eliminate delegate allocation and reduce
/// indirection in the hot path. Public-facing delegates are created only once at
/// build time as thin facades that jump directly to these function pointers.
/// </para>
/// <para>
/// Requirements for a packet type:
/// <list type="bullet">
///   <item>Implements <see cref="IPacket"/>.</item>
///   <item>Implements the static abstract members defined by
///         <see cref="IPacketTransformer{TPacket}"/> on the concrete packet type:
///         <c>FromBytes(ReadOnlySpan&lt;byte&gt;)</c>, <c>Compress(TPacket)</c>,
///         <c>Decompress(TPacket)</c>, <c>Encrypt(TPacket, byte[], CipherType)</c>,
///         <c>Decrypt(TPacket, byte[], CipherType)</c>.</item>
/// </list>
/// </para>
/// <para>
/// If <see cref="PipelineManagedTransformAttribute"/> is present on a packet type,
/// transformer binding is skipped (deserialize may still be bound).
/// </para>
/// <para>
/// When trimming or AOT compiling, ensure these static methods are preserved using
/// <c>DynamicDependency</c> or <c>DynamicallyAccessedMembers</c> as appropriate.
/// </para>
/// </remarks>
[System.Diagnostics.DebuggerDisplay("C={HasCompress}, D={HasDecompress}, E={HasEncrypt}, R={HasDecrypt}")]
public sealed class PacketRegistryFactory
{
    #region Static: Defaults & Utilities

    private const System.Reflection.BindingFlags BindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;

    private static readonly System.Reflection.MethodInfo BindAllPtrsMi;
    private static readonly System.Collections.Generic.HashSet<System.String> Namespaces;

    #endregion Static: Defaults & Utilities

    #region Fields

    private readonly System.Collections.Generic.HashSet<System.Type> _explicitPacketTypes = [];
    private readonly System.Collections.Generic.HashSet<System.Reflection.Assembly> _assemblies = [];

    #endregion Fields

    #region Constructors

    static PacketRegistryFactory()
    {
        Namespaces = new(System.StringComparer.Ordinal)
        {
            typeof(Text256).Namespace!,
            typeof(Control).Namespace!
        };

        BindAllPtrsMi = typeof(PacketRegistryFactory).GetMethod(nameof(BindPtrs), BindingFlags)!;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PacketRegistryFactory"/> and registers
    /// built-in packet types.
    /// </summary>
    public PacketRegistryFactory()
    {
        // Text packets
        _ = this.RegisterPacket<Text256>()
                .RegisterPacket<Text512>()
                .RegisterPacket<Text1024>();

        // Control / handshake packets
        _ = this.RegisterPacket<Control>()
                .RegisterPacket<Handshake>()
                .RegisterPacket<Directive>();
    }

    #endregion Constructors

    #region API

    /// <summary>
    /// Builds an immutable catalog of packet deserializers and transformers.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when duplicate magic numbers are detected.
    /// </exception>
    public PacketRegistry CreateCatalog()
    {
        // Pre-allocate to reduce rehashing.
        System.Int32 estimated =
            System.Math.Max(16, _explicitPacketTypes.Count + System.Math.Min(64, _assemblies.Count * 8));

        System.Collections.Generic.Dictionary<System.Type, PacketTransformer> transformers = new(estimated);
        System.Collections.Generic.Dictionary<System.UInt32, PacketDeserializer> deserializers = new(estimated);

        // 1) Collect candidate packet types
        System.Collections.Generic.HashSet<System.Type> candidates = [.. _explicitPacketTypes];

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SH.{nameof(PacketRegistryFactory)}] " +
                                      $"build-start asm={_assemblies.Count} explicit={_explicitPacketTypes.Count}");

        foreach (System.Reflection.Assembly asm in _assemblies)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[SH.{nameof(PacketRegistryFactory)}] scan-asm name={asm.FullName}");

            foreach (System.Type? type in SafeGetTypes(asm))
            {
                if (type?.IsClass != true || type.IsAbstract)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[SH.{nameof(PacketRegistryFactory)}] skip reason=not-class type={type?.Name}");
                    continue;
                }

                if (!_explicitPacketTypes.Contains(type) &&
                    type.Namespace is not null && Namespaces.Contains(type.Namespace))
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[SH.{nameof(PacketRegistryFactory)}] skip reason=default-ns type={type?.Name}");
                    continue;
                }

                if (!typeof(IPacket).IsAssignableFrom(type))
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[SH.{nameof(PacketRegistryFactory)}] skip reason=not-ipacket type={type?.Name}");
                    continue;
                }

                _ = candidates.Add(type);
            }
        }

        if (candidates.Count == 0)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[SH.{nameof(PacketRegistryFactory)}] no-candidate");
        }

        // 2) Bind per type
        const System.Reflection.BindingFlags FLAGS = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;

        foreach (System.Type type in candidates)
        {
            // Magic number
            System.UInt32 key = PacketRegistryFactory.Compute(type);

            // Pipeline-managed?
            System.Boolean pipelineManaged = type.IsDefined(typeof(PipelineManagedTransformAttribute), inherit: false);

            // Locate static implementations — search concrete type first, then climb
            // the inheritance chain. Inherited static methods are not returned by
            // GetMethod without FlattenHierarchy, and FlattenHierarchy cannot resolve
            // generic base types correctly, so we walk manually.
            System.Reflection.MethodInfo? facadeDeserializeMi = FindStaticMethod(
                type, FLAGS,
                nameof(IPacketDeserializer<>.Deserialize),
                [typeof(System.ReadOnlySpan<System.Byte>)]);

            System.Reflection.MethodInfo? facadeEncryptMi = FindStaticMethod(
                type, FLAGS,
                nameof(IPacketEncryptor<>.Encrypt),
                [type, typeof(System.Byte[]), typeof(CipherSuiteType)]);

            System.Reflection.MethodInfo? facadeDecryptMi = FindStaticMethod(
                type, FLAGS,
                nameof(IPacketEncryptor<>.Decrypt),
                [type, typeof(System.Byte[])]);

            System.Reflection.MethodInfo? facadeCompressMi = FindStaticMethod(
                type, FLAGS,
                nameof(IPacketCompressor<>.Compress),
                [type]);

            System.Reflection.MethodInfo? facadeDecompressMi = FindStaticMethod(
                type, FLAGS,
                nameof(IPacketCompressor<>.Decompress),
                [type]);

            // ---- Deserializer binding (required if magic exists) ----
            if (facadeDeserializeMi is not null)
            {
                if (deserializers.ContainsKey(key))
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Fatal($"[SH.{nameof(PacketRegistryFactory)}] dup-magic val=0x{key:X8} type={type?.Name}");
                }

                // Assign FromBytes pointer into Fn<TPacket>
                _ = BindAllPtrsMi.MakeGenericMethod(type!).Invoke(null, [facadeDeserializeMi, null, null, null, null]);

                // Build a stable PacketDeserializer that jumps to Fn<T>.FromBytes
                System.Type tGeneric = typeof(PacketFunctionTable<>).MakeGenericType(type!);
                System.Reflection.MethodInfo doDeserializeMi = tGeneric.GetMethod(nameof(PacketFunctionTable<>.InvokeDeserialize), FLAGS)!;

                // Create an actual delegate instance once (no reflection in hot path)
                PacketDeserializer deserFacade = (PacketDeserializer)System.Delegate.CreateDelegate(typeof(PacketDeserializer), doDeserializeMi);

                deserializers[key] = deserFacade;
            }
            else
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[SH.{nameof(PacketRegistryFactory)}] miss-deserialize type={type?.Name}");
                continue;
            }

            // ---- Transformer binding ----
            if (pipelineManaged)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[SH.{nameof(PacketRegistryFactory)}] pipeline-managed type={type?.Name}");

                transformers[type!] = new PacketTransformer(null, null, null, null);

                continue;
            }

            // Assign all pointers into Fn<TPacket>
            _ = BindAllPtrsMi.MakeGenericMethod(type!).Invoke(null, [null, facadeCompressMi, facadeDecompressMi, facadeEncryptMi, facadeDecryptMi]);

            // Create public-facing delegates once (jump to Fn<T>.DoXXX)
            System.Type fnType = typeof(PacketFunctionTable<>).MakeGenericType(type!);

            System.Func<IPacket, IPacket>? compressDel = null;
            System.Func<IPacket, IPacket>? decompressDel = null;
            System.Func<IPacket, System.Byte[], IPacket>? decryptDel = null;
            System.Func<IPacket, System.Byte[], CipherSuiteType, IPacket>? encryptDel = null;

            System.Reflection.MethodInfo invokeEncryptMi = fnType.GetMethod(nameof(PacketFunctionTable<>.InvokeEncrypt), FLAGS)!;
            System.Reflection.MethodInfo invokeDecryptMi = fnType.GetMethod(nameof(PacketFunctionTable<>.InvokeDecrypt), FLAGS)!;
            System.Reflection.MethodInfo invokeCompressMi = fnType.GetMethod(nameof(PacketFunctionTable<>.InvokeCompress), FLAGS)!;
            System.Reflection.MethodInfo invokeDecompressMi = fnType.GetMethod(nameof(PacketFunctionTable<>.InvokeDecompress), FLAGS)!;

            if (facadeCompressMi is not null)
            {
                compressDel = (System.Func<IPacket, IPacket>)System.Delegate.CreateDelegate(typeof(System.Func<IPacket, IPacket>), invokeCompressMi);
            }

            if (facadeDecompressMi is not null)
            {
                decompressDel = (System.Func<IPacket, IPacket>)System.Delegate.CreateDelegate(typeof(System.Func<IPacket, IPacket>), invokeDecompressMi);
            }

            if (facadeEncryptMi is not null)
            {
                encryptDel = (System.Func<IPacket, System.Byte[], CipherSuiteType, IPacket>)
                System.Delegate.CreateDelegate(typeof(System.Func<IPacket, System.Byte[], CipherSuiteType, IPacket>), invokeEncryptMi);
            }

            if (facadeDecryptMi is not null)
            {
                decryptDel = (System.Func<IPacket, System.Byte[], IPacket>)
                System.Delegate.CreateDelegate(typeof(System.Func<IPacket, System.Byte[], IPacket>), invokeDecryptMi);
            }

            transformers[type!] = new PacketTransformer(compressDel, decompressDel, encryptDel, decryptDel);
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[SH.{nameof(PacketRegistryFactory)}] build-ok packets={deserializers.Count} transformers={transformers.Count}");

        // Freeze for thread-safe, allocation-free lookups
        return new PacketRegistry(
            System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(transformers),
            System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(deserializers));
    }

    /// <summary>
    /// Registers a concrete packet type explicitly (skipped from scanning).
    /// </summary>
    public PacketRegistryFactory RegisterPacket<TPacket>() where TPacket : IPacket
    {
        System.Type t = typeof(TPacket);

        if (_explicitPacketTypes.Add(typeof(TPacket)))
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[SH.{nameof(PacketRegistryFactory)}] reg-type type={t.Name}");
        }
        else
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[SH.{nameof(PacketRegistryFactory)}] reg-type-skip type={t.Name}");
        }

        return this;
    }

    /// <summary>
    /// Adds an assembly to be scanned for packet types.
    /// </summary>
    public PacketRegistryFactory IncludeAssembly(System.Reflection.Assembly? asm)
    {
        if (asm is null)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[SH.{nameof(PacketRegistryFactory)}] include-asm-null");
            return this;
        }

        if (_assemblies.Add(asm))
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[SH.{nameof(PacketRegistryFactory)}] include-asm name={asm.FullName}");
        }
        else
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[SH.{nameof(PacketRegistryFactory)}] include-asm-skip name={asm.FullName}");
        }

        return this;
    }

    /// <summary>
    /// Computes a stable, deterministic 32-bit key from the type's full name
    /// using FNV-1a. Consistent across machines, processes, and .NET versions
    /// as long as the type name does not change.
    /// </summary>
    public static System.UInt32 Compute(System.Type type)
    {
        // Use AssemblyQualifiedName for maximum uniqueness,
        // or FullName if you want assembly-agnostic keys.
        System.ReadOnlySpan<System.Char> name = System.MemoryExtensions.AsSpan(type.FullName);

        // FNV-1a 32-bit
        System.UInt32 hash = 2166136261u; // FNV offset basis
        foreach (System.Char c in name)
        {
            hash ^= c;
            hash *= 16777619u;   // FNV prime
        }
        return hash;
    }

    #endregion API

    #region Private Methods

    /// <summary>
    /// Generic trampoline that stores function pointers for a specific <typeparamref name="TPacket"/>.
    /// The public static methods act as thin facades converting <see cref="IPacket"/> to/from
    /// <typeparamref name="TPacket"/> and then invoking the function pointer.
    /// </summary>
    private static unsafe class PacketFunctionTable<TPacket> where TPacket : IPacket
    {
        // Note: The 'in' modifier on ReadOnlySpan is optional in the consumer; the function pointer
        // signature must match the actual static method on TPacket. If your methods use
        // 'in ReadOnlySpan<byte>' exactly, it remains ABI-compatible to call with a plain argument.
        public static delegate* managed<System.ReadOnlySpan<System.Byte>, TPacket> DeserializePtr;

        public static delegate* managed<TPacket, TPacket> CompressPtr;
        public static delegate* managed<TPacket, TPacket> DecompressPtr;
        public static delegate* managed<TPacket, System.Byte[], CipherSuiteType, TPacket> EncryptPtr;
        public static delegate* managed<TPacket, System.Byte[], TPacket> DecryptPtr;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket InvokeDeserialize(System.ReadOnlySpan<System.Byte> raw) => DeserializePtr(raw);

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket InvokeCompress(IPacket p) => CompressPtr((TPacket)p);

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket InvokeDecompress(IPacket p) => DecompressPtr((TPacket)p);

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket InvokeEncrypt(IPacket p, System.Byte[] key, CipherSuiteType alg) => EncryptPtr((TPacket)p, key, alg);

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket InvokeDecrypt(IPacket p, System.Byte[] key) => DecryptPtr((TPacket)p, key);
    }

    /// <summary>
    /// Searches for a static method by name and parameter types on <paramref name="startType"/>,
    /// then walks up the inheritance chain if not found on the concrete type.
    /// <para>
    /// This is necessary because <c>GetMethod</c> with <c>Public | Static</c> does not return
    /// inherited static methods from generic base types (e.g. <c>PacketBase&lt;TSelf&gt;</c>).
    /// </para>
    /// If the method found on a base type is a generic method definition, it is closed
    /// over <paramref name="startType"/> automatically.
    /// </summary>
    private static System.Reflection.MethodInfo? FindStaticMethod(
        System.Type startType,
        System.Reflection.BindingFlags flags,
        System.String name,
        System.Type[] parameterTypes)
    {
        System.Type? current = startType;
        while (current is not null && current != typeof(System.Object))
        {
            System.Reflection.MethodInfo? mi = current.GetMethod(
                name: name,
                bindingAttr: flags,
                binder: null,
                types: parameterTypes,
                modifiers: null);

            if (mi is not null)
            {
                // Close over the concrete type when the method is generic (e.g. Deserialize<TSelf>)
                return mi.IsGenericMethodDefinition
                    ? mi.MakeGenericMethod(startType)
                    : mi;
            }

            current = current.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Safely iterates types of an assembly, even if some fail to load.
    /// </summary>
    private static System.Collections.Generic.IEnumerable<System.Type> SafeGetTypes(System.Reflection.Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (System.Reflection.ReflectionTypeLoadException ex)
        {
            return System.Linq.Enumerable.OfType<System.Type>(ex.Types);
        }
        catch
        {
            return [];
        }
    }

    #region Binding Helpers

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate* managed<TPacket, TPacket> BindUnaryPtr<TPacket>(System.Reflection.MethodInfo mi)
    {
        System.IntPtr ptr = mi.MethodHandle.GetFunctionPointer();
        return (delegate* managed<TPacket, TPacket>)ptr;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate* managed<TPacket, System.Byte[], CipherSuiteType, TPacket> BindEncryptPtr<TPacket>(System.Reflection.MethodInfo mi)
    {
        System.IntPtr ptr = mi.MethodHandle.GetFunctionPointer();
        return (delegate* managed<TPacket, System.Byte[], CipherSuiteType, TPacket>)ptr;
    }

    [System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate* managed<TPacket, System.Byte[], TPacket> BindDecryptPtr<TPacket>(System.Reflection.MethodInfo mi)
    {
        System.IntPtr ptr = mi.MethodHandle.GetFunctionPointer();
        return (delegate* managed<TPacket, System.Byte[], TPacket>)ptr;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate* managed<System.ReadOnlySpan<System.Byte>, TPacket> BindDeserializePtr<TPacket>(System.Reflection.MethodInfo mi)
    {
        System.IntPtr ptr = mi.MethodHandle.GetFunctionPointer();
        return (delegate* managed<System.ReadOnlySpan<System.Byte>, TPacket>)ptr;
    }

    #endregion Binding Helpers

    /// <summary>
    /// Assigns function pointers to <see cref="PacketFunctionTable{TPacket}"/> for a specific packet type.
    /// Any null <paramref name="miDeserialize"/>, <paramref name="miCompress"/>,
    /// <paramref name="miDecompress"/>, <paramref name="miEncrypt"/>, or <paramref name="miDecrypt"/> parameters are skipped.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe void BindPtrs<TPacket>(
        System.Reflection.MethodInfo? miDeserialize,
        System.Reflection.MethodInfo? miCompress,
        System.Reflection.MethodInfo? miDecompress,
        System.Reflection.MethodInfo? miEncrypt,
        System.Reflection.MethodInfo? miDecrypt) where TPacket : IPacket
    {
        if (miDeserialize is not null)
        {
            PacketFunctionTable<TPacket>.DeserializePtr = BindDeserializePtr<TPacket>(miDeserialize);
        }

        if (miCompress is not null)
        {
            PacketFunctionTable<TPacket>.CompressPtr = BindUnaryPtr<TPacket>(miCompress);
        }

        if (miDecompress is not null)
        {
            PacketFunctionTable<TPacket>.DecompressPtr = BindUnaryPtr<TPacket>(miDecompress);
        }

        if (miEncrypt is not null)
        {
            PacketFunctionTable<TPacket>.EncryptPtr = BindEncryptPtr<TPacket>(miEncrypt);
        }

        if (miDecrypt is not null)
        {
            PacketFunctionTable<TPacket>.DecryptPtr = BindDecryptPtr<TPacket>(miDecrypt);
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?.Meta(
            $"[SH.{nameof(PacketRegistryFactory)}] bind " +
            $"type={typeof(TPacket).Name} des={(miDeserialize is not null ? "+" : "-")} " +
            $"cmp={(miCompress is not null ? "+" : "-")} dcmp={(miDecompress is not null ? "+" : "-")} " +
            $"enc={(miEncrypt is not null ? "+" : "-")} dec={(miDecrypt is not null ? "+" : "-")}");
    }

    #endregion Private Methods
}