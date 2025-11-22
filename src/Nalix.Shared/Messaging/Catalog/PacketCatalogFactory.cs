// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Enums;
using Nalix.Common.Logging;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Shared.Messaging.Controls;
using Nalix.Shared.Messaging.Text;

namespace Nalix.Shared.Messaging.Catalog;

/// <summary>
/// Builds an immutable <see cref="PacketCatalog"/> by scanning packet types and
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
///   <item>Decorated with <see cref="MagicNumberAttribute"/>.</item>
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
public sealed class PacketCatalogFactory
{
    #region Static: Defaults & Utilities

    /// <summary>
    /// Default namespaces to skip when scanning assemblies (built-in packet types).
    /// </summary>
    private static readonly System.Collections.Generic.HashSet<System.String> Namespaces = new(
        System.Linq.Enumerable.Where(
        [
            typeof(Text256).Namespace!,
            typeof(Control).Namespace!
        ], ns => ns is not null),
        System.StringComparer.Ordinal);

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

    private static readonly System.Reflection.MethodInfo BindAllPtrsMi = typeof(PacketCatalogFactory).GetMethod(
        nameof(BindAllPtrsGeneric),
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

    #endregion

    #region Fields

    private readonly System.Collections.Generic.HashSet<System.Type> _explicitPacketTypes = [];
    private readonly System.Collections.Generic.HashSet<System.Reflection.Assembly> _assemblies = [];

    #endregion

    #region Ctor & Registration

    /// <summary>
    /// Initializes a new instance of <see cref="PacketCatalogFactory"/> and registers
    /// built-in packet types.
    /// </summary>
    public PacketCatalogFactory()
    {
        // Text packets
        _ = this.RegisterPacket<Text256>()
                .RegisterPacket<Text512>()
                .RegisterPacket<Text1024>();

        // CONTROL / handshake packets
        _ = this.RegisterPacket<Control>()
                .RegisterPacket<Handshake>()
                .RegisterPacket<Directive>();
    }

    /// <summary>
    /// Adds an assembly to be scanned for packet types.
    /// </summary>
    public PacketCatalogFactory IncludeAssembly(System.Reflection.Assembly? asm)
    {
        if (asm is null)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(PacketCatalogFactory)}] include-asm-null");
            return this;
        }

        if (_assemblies.Add(asm))
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(PacketCatalogFactory)}] include-asm name={asm.FullName}");
        }
        else
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(PacketCatalogFactory)}] include-asm-skip name={asm.FullName}");
        }

        return this;
    }

    /// <summary>
    /// Registers a concrete packet type explicitly (skipped from scanning).
    /// </summary>
    public PacketCatalogFactory RegisterPacket<TPacket>() where TPacket : IPacket
    {
        System.Type t = typeof(TPacket);

        if (_explicitPacketTypes.Add(typeof(TPacket)))
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(PacketCatalogFactory)}] reg-type type={t.Name}");
        }
        else
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[{nameof(PacketCatalogFactory)}] reg-type-skip type={t.Name}");
        }

        return this;
    }

    #endregion

    #region Unsafe Trampoline & Binders

    /// <summary>
    /// Generic trampoline that stores function pointers for a specific <typeparamref name="TPacket"/>.
    /// The public static methods act as thin facades converting <see cref="IPacket"/> to/from
    /// <typeparamref name="TPacket"/> and then invoking the function pointer.
    /// </summary>
    private static unsafe class Fn<TPacket> where TPacket : IPacket
    {
        // Note: The 'in' modifier on ReadOnlySpan is optional in the consumer; the function pointer
        // signature must match the actual static method on TPacket. If your methods use
        // 'in ReadOnlySpan<byte>' exactly, it remains ABI-compatible to call with a plain argument.
        public static delegate* managed<System.ReadOnlySpan<System.Byte>, TPacket> Deserialize;

        public static delegate* managed<TPacket, TPacket> Compress;
        public static delegate* managed<TPacket, TPacket> Decompress;
        public static delegate* managed<TPacket, System.Byte[], CipherSuiteType, TPacket> Encrypt;
        public static delegate* managed<TPacket, System.Byte[], CipherSuiteType, TPacket> Decrypt;

        /// <summary>
        /// Facade for <see cref="PacketDeserializer"/>.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket DoDeserialize(System.ReadOnlySpan<System.Byte> raw) => Deserialize(raw);

        /// <summary>
        /// Facade for <see cref="PacketTransformer.Compress"/>.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket DoCompress(IPacket p) => Compress((TPacket)p);

        /// <summary>
        /// Facade for <see cref="PacketTransformer.Decompress"/>.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket DoDecompress(IPacket p) => Decompress((TPacket)p);

        /// <summary>
        /// Facade for <see cref="PacketTransformer.Encrypt"/>.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket DoEncrypt(IPacket p, System.Byte[] key, CipherSuiteType alg) => Encrypt((TPacket)p, key, alg);

        /// <summary>
        /// Facade for <see cref="PacketTransformer.Decrypt"/>.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket DoDecrypt(IPacket p, System.Byte[] key, CipherSuiteType alg) => Decrypt((TPacket)p, key, alg);
    }

    private static unsafe delegate* managed<TPacket, TPacket> ToUnaryPtr<TPacket>(System.Reflection.MethodInfo mi)
    {
        System.IntPtr ptr = mi.MethodHandle.GetFunctionPointer();
        return (delegate* managed<TPacket, TPacket>)ptr;
    }

    private static unsafe delegate* managed<TPacket, System.Byte[], CipherSuiteType, TPacket>
        ToCryptoPtr<TPacket>(System.Reflection.MethodInfo mi)
    {
        System.IntPtr ptr = mi.MethodHandle.GetFunctionPointer();
        return (delegate* managed<TPacket, System.Byte[], CipherSuiteType, TPacket>)ptr;
    }

    private static unsafe delegate* managed<System.ReadOnlySpan<System.Byte>, TPacket>
        ToDeserializePtr<TPacket>(System.Reflection.MethodInfo mi)
    {
        System.IntPtr ptr = mi.MethodHandle.GetFunctionPointer();
        return (delegate* managed<System.ReadOnlySpan<System.Byte>, TPacket>)ptr;
    }

    /// <summary>
    /// Assigns function pointers to <see cref="Fn{TPacket}"/> for a specific packet type.
    /// Any null <paramref name="miDeserialize"/>, <paramref name="miCompress"/>,
    /// <paramref name="miDecompress"/>, <paramref name="miEncrypt"/>, or <paramref name="miDecrypt"/> parameters are skipped.
    /// </summary>
    private static unsafe void BindAllPtrsGeneric<TPacket>(
        System.Reflection.MethodInfo? miDeserialize,
        System.Reflection.MethodInfo? miCompress,
        System.Reflection.MethodInfo? miDecompress,
        System.Reflection.MethodInfo? miEncrypt,
        System.Reflection.MethodInfo? miDecrypt)
        where TPacket : IPacket
    {
        if (miDeserialize is not null)
        {
            Fn<TPacket>.Deserialize = ToDeserializePtr<TPacket>(miDeserialize);
        }

        if (miCompress is not null)
        {
            Fn<TPacket>.Compress = ToUnaryPtr<TPacket>(miCompress);
        }

        if (miDecompress is not null)
        {
            Fn<TPacket>.Decompress = ToUnaryPtr<TPacket>(miDecompress);
        }

        if (miEncrypt is not null)
        {
            Fn<TPacket>.Encrypt = ToCryptoPtr<TPacket>(miEncrypt);
        }

        if (miDecrypt is not null)
        {
            Fn<TPacket>.Decrypt = ToCryptoPtr<TPacket>(miDecrypt);
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?.Meta(
            $"[{nameof(PacketCatalogFactory)}] bind " +
            $"type={typeof(TPacket).Name} " +
            $"des={(miDeserialize is not null ? "+" : "-")} " +
            $"cmp={(miCompress is not null ? "+" : "-")} " +
            $"dcmp={(miDecompress is not null ? "+" : "-")} " +
            $"enc={(miEncrypt is not null ? "+" : "-")} " +
            $"dec={(miDecrypt is not null ? "+" : "-")}");
    }

    #endregion

    #region Build

    /// <summary>
    /// Builds an immutable catalog of packet deserializers and transformers.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when duplicate magic numbers are detected.
    /// </exception>
    public PacketCatalog CreateCatalog()
    {
        // Pre-allocate to reduce rehashing.
        System.Int32 estimated =
            System.Math.Max(16, _explicitPacketTypes.Count + System.Math.Min(64, _assemblies.Count * 8));

        System.Collections.Generic.Dictionary<System.Type, PacketTransformer> transformers = new(estimated);
        System.Collections.Generic.Dictionary<System.UInt32, PacketDeserializer> deserializers = new(estimated);

        // 1) Collect candidate packet types
        System.Collections.Generic.HashSet<System.Type> candidates = [.. _explicitPacketTypes];

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[{nameof(PacketCatalogFactory)}] " +
                                      $"build-start asm={_assemblies.Count} explicit={_explicitPacketTypes.Count}");

        foreach (System.Reflection.Assembly asm in _assemblies)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(PacketCatalogFactory)}] scan-asm name={asm.FullName}");

            foreach (System.Type? type in SafeGetTypes(asm))
            {
                if (type?.IsClass != true || type.IsAbstract)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[{nameof(PacketCatalogFactory)}] " +
                                                   $"skip reason=not-class type={type?.Name}");
                    continue;
                }

                if (type.Namespace is not null && Namespaces.Contains(type.Namespace))
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[{nameof(PacketCatalogFactory)}] " +
                                                   $"skip reason=default-ns type={type?.Name}");
                    continue;
                }

                if (!typeof(IPacket).IsAssignableFrom(type))
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[{nameof(PacketCatalogFactory)}] " +
                                                   $"skip reason=not-ipacket type={type?.Name}");
                    continue;
                }

                _ = candidates.Add(type);
            }
        }

        if (candidates.Count == 0)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(PacketCatalogFactory)}] no-candidate");
        }

        // 2) Bind per type
        const System.Reflection.BindingFlags FLAGS = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;

        foreach (System.Type type in candidates)
        {
            // Magic number
            var magicAttr = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<MagicNumberAttribute>(type, inherit: false);
            if (magicAttr is null)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[{nameof(PacketCatalogFactory)}] " +
                                               $"skip reason=no-magic type={type?.Name}");
                continue;
            }

            // Pipeline-managed?
            System.Boolean pipelineManaged = type.IsDefined(typeof(PipelineManagedTransformAttribute), inherit: false);

            // Locate static abstract implementations on the concrete packet type
            System.Reflection.MethodInfo? miDeserialize = type.GetMethod(
                name: nameof(IPacketDeserializer<IPacket>.Deserialize),
                bindingAttr: FLAGS,
                binder: null,
                types: [typeof(System.ReadOnlySpan<System.Byte>)],
                modifiers: null);

            System.Reflection.MethodInfo? miCompress = type.GetMethod(
                name: nameof(IPacketCompressor<IPacket>.Compress),
                bindingAttr: FLAGS,
                binder: null,
                types: [type],
                modifiers: null);

            System.Reflection.MethodInfo? miDecompress = type.GetMethod(
                name: nameof(IPacketCompressor<IPacket>.Decompress),
                bindingAttr: FLAGS,
                binder: null,
                types: [type],
                modifiers: null);

            System.Reflection.MethodInfo? miEncrypt = type.GetMethod(
                name: nameof(IPacketEncryptor<IPacket>.Encrypt),
                bindingAttr: FLAGS,
                binder: null,
                types: [type, typeof(System.Byte[]), typeof(CipherSuiteType)],
                modifiers: null);

            System.Reflection.MethodInfo? miDecrypt = type.GetMethod(
                name: nameof(IPacketEncryptor<IPacket>.Decrypt),
                bindingAttr: FLAGS,
                binder: null,
                types: [type, typeof(System.Byte[]), typeof(CipherSuiteType)],
                modifiers: null);

            // ---- Deserializer binding (required if magic exists) ----
            if (miDeserialize is not null)
            {
                if (deserializers.ContainsKey(magicAttr.MagicNumber))
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Fatal($"[{nameof(PacketCatalogFactory)}] " +
                                                   $"dup-magic val=0x{magicAttr.MagicNumber:X8} type={type?.Name}");
                }

                // Assign FromBytes pointer into Fn<TPacket>
                _ = BindAllPtrsMi.MakeGenericMethod(type!).Invoke(null, [miDeserialize, null, null, null, null]);

                // Build a stable PacketDeserializer that jumps to Fn<T>.FromBytes
                var tGeneric = typeof(Fn<>).MakeGenericType(type!);
                var doDeserializeMi = tGeneric.GetMethod(nameof(Fn<IPacket>.DoDeserialize), FLAGS)!;

                // Create an actual delegate instance once (no reflection in hot path)
                var deserFacade = (PacketDeserializer)System.Delegate.CreateDelegate(
                    typeof(PacketDeserializer), doDeserializeMi);

                deserializers[magicAttr.MagicNumber] = deserFacade;
            }
            else
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(PacketCatalogFactory)}] " +
                                               $"miss-deserialize type={type?.Name}");
                continue;
            }

            // ---- Transformer binding ----
            if (pipelineManaged)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[{nameof(PacketCatalogFactory)}] " +
                                               $"pipeline-managed type={type?.Name}");

                transformers[type!] = new PacketTransformer(null, null, null, null);

                continue;
            }

            // Assign all pointers into Fn<TPacket>
            _ = BindAllPtrsMi.MakeGenericMethod(type!).Invoke(null, [null, miCompress, miDecompress, miEncrypt, miDecrypt]);

            // Create public-facing delegates once (jump to Fn<T>.DoXXX)
            System.Type fnType = typeof(Fn<>).MakeGenericType(type!);

            System.Func<IPacket, IPacket>? compressDel = null;
            System.Func<IPacket, IPacket>? decompressDel = null;
            System.Func<IPacket, System.Byte[], CipherSuiteType, IPacket>? encryptDel = null;
            System.Func<IPacket, System.Byte[], IPacket>? decryptDel = null;

            System.Reflection.MethodInfo doEncryptMi = fnType.GetMethod(nameof(Fn<IPacket>.DoEncrypt), FLAGS)!;
            System.Reflection.MethodInfo doDecryptMi = fnType.GetMethod(nameof(Fn<IPacket>.DoDecrypt), FLAGS)!;
            System.Reflection.MethodInfo doCompressMi = fnType.GetMethod(nameof(Fn<IPacket>.DoCompress), FLAGS)!;
            System.Reflection.MethodInfo doDecompressMi = fnType.GetMethod(nameof(Fn<IPacket>.DoDecompress), FLAGS)!;

            if (miCompress is not null)
            {
                compressDel = (System.Func<IPacket, IPacket>)System.Delegate.CreateDelegate(typeof(System.Func<IPacket, IPacket>), doCompressMi);
            }

            if (miDecompress is not null)
            {
                decompressDel = (System.Func<IPacket, IPacket>)System.Delegate.CreateDelegate(typeof(System.Func<IPacket, IPacket>), doDecompressMi);
            }

            if (miEncrypt is not null)
            {
                encryptDel = (System.Func<IPacket, System.Byte[], CipherSuiteType, IPacket>)
                System.Delegate.CreateDelegate(typeof(System.Func<IPacket, System.Byte[], CipherSuiteType, IPacket>), doEncryptMi);
            }

            if (miDecrypt is not null)
            {
                decryptDel = (System.Func<IPacket, System.Byte[], IPacket>)
                System.Delegate.CreateDelegate(typeof(System.Func<IPacket, System.Byte[], IPacket>), doDecryptMi);
            }

            transformers[type!] = new PacketTransformer(compressDel, decompressDel, encryptDel, decryptDel);
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[{nameof(PacketCatalogFactory)}] build-ok packets={deserializers.Count} transformers={transformers.Count}");

        // Freeze for thread-safe, allocation-free lookups
        return new PacketCatalog(
            System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(transformers),
            System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(deserializers));
    }

    #endregion
}
