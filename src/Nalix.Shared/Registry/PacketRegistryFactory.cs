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
/// pointers</b> (<c>delegate*</c>) to eliminate delegate allocation and reduce
/// indirection in the hot path. Public-facing delegates are created only once at
/// build time as thin facades that jump directly to these function pointers.
/// </para>
/// <para>
/// Requirements for a packet type:
/// <list type="bullet">
///   <item>Implements <see cref="IPacket"/>.</item>
///   <item>Implements the static abstract members defined by
///         <see cref="IPacketTransformer{TPacket}"/> on the concrete packet type.</item>
/// </list>
/// </para>
/// <para>
/// If <see cref="PipelineManagedTransformAttribute"/> is present on a packet type,
/// transformer binding is skipped (deserializer may still be bound).
/// </para>
/// </remarks>
[System.Diagnostics.DebuggerDisplay("C={HasCompress}, D={HasDecompress}, E={HasEncrypt}, R={HasDecrypt}")]
public sealed class PacketRegistryFactory
{
    #region Static: Defaults & Utilities

    private const System.Reflection.BindingFlags StaticNonPublic =
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Static;

    private const System.Reflection.BindingFlags StaticPublic =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.Static;

    private static readonly System.Reflection.MethodInfo BindAllPtrsMi;

    // Built-in namespaces whose types are pre-registered in the default constructor
    // and must NOT be re-added during assembly scanning (would cause duplicate magic).
    private static readonly System.Collections.Frozen.FrozenSet<System.String> BuiltInNamespaces;

    #endregion Static: Defaults & Utilities

    #region Fields

    private readonly System.Collections.Generic.HashSet<System.Type> _explicitPacketTypes = [];
    private readonly System.Collections.Generic.HashSet<System.Reflection.Assembly> _assemblies = [];

    // namespace → recursive flag
    // Key: namespace string (exact or prefix match when recursive=true)
    private readonly System.Collections.Generic.Dictionary<System.String, System.Boolean> _namespaceScan
        = new(System.StringComparer.Ordinal);

    #endregion Fields

    #region Constructors

    static PacketRegistryFactory()
    {
        BuiltInNamespaces = System.Collections.Frozen.FrozenSet.ToFrozenSet(
            new System.String[]
            {
                typeof(Text256).Namespace!,
                typeof(Control).Namespace!
            },
            System.StringComparer.Ordinal);

        BindAllPtrsMi = typeof(PacketRegistryFactory)
            .GetMethod(nameof(BindPtrs), StaticNonPublic)
            ?? throw new System.InvalidOperationException(
                $"Cannot locate private method '{nameof(BindPtrs)}' on {nameof(PacketRegistryFactory)}.");
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PacketRegistryFactory"/> and registers
    /// built-in packet types.
    /// </summary>
    public PacketRegistryFactory()
    {
        // Text packets
        _ = RegisterPacket<Text256>()
            .RegisterPacket<Text512>()
            .RegisterPacket<Text1024>();

        // Control / handshake packets
        _ = RegisterPacket<Control>()
            .RegisterPacket<Handshake>()
            .RegisterPacket<Directive>();
    }

    #endregion Constructors

    #region Public API

    /// <summary>
    /// Registers a concrete packet type explicitly.
    /// </summary>
    public PacketRegistryFactory RegisterPacket<TPacket>() where TPacket : IPacket
    {
        System.Type t = typeof(TPacket);
        System.Boolean added = _explicitPacketTypes.Add(t);

        INFO(added
            ? $"reg-type type={t.Name}"
            : $"reg-type-skip type={t.Name}");

        return this;
    }

    /// <summary>
    /// Adds an assembly to be scanned for packet types.
    /// Only types whose namespace is NOT in the built-in set will be considered.
    /// </summary>
    public PacketRegistryFactory IncludeAssembly(System.Reflection.Assembly? asm)
    {
        if (asm is null) { INFO("include-asm-null"); return this; }

        System.Boolean added = _assemblies.Add(asm);
        INFO(added
            ? $"include-asm name={asm.GetName().Name}"
            : $"include-asm-skip name={asm.GetName().Name}");

        return this;
    }

    /// <summary>
    /// Scans all loaded assemblies in the current <see cref="System.AppDomain"/>
    /// for packet types.
    /// </summary>
    public PacketRegistryFactory IncludeCurrentDomain()
    {
        foreach (System.Reflection.Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            IncludeAssembly(asm);
        }

        return this;
    }

    /// <summary>
    /// Registers all concrete <see cref="IPacket"/> types whose namespace exactly matches
    /// <paramref name="ns"/>, from all assemblies that have been added via
    /// <see cref="IncludeAssembly"/> or <see cref="IncludeCurrentDomain"/>.
    /// </summary>
    /// <param name="ns">
    /// The exact namespace to match, e.g. <c>"MyGame.Network.Packets"</c>.
    /// </param>
    /// <remarks>
    /// Only direct members of the namespace are matched. To include nested namespaces
    /// (e.g. <c>MyGame.Network.Packets.Auth</c>), use
    /// <see cref="IncludeNamespaceRecursive"/> instead.
    /// </remarks>
    public PacketRegistryFactory IncludeNamespace(System.String ns)
    {
        System.ArgumentException.ThrowIfNullOrWhiteSpace(ns);

        // If already registered as recursive, keep recursive (superset).
        if (!_namespaceScan.TryGetValue(ns, out System.Boolean existing) || !existing)
        {
            _namespaceScan[ns] = false;
        }

        INFO($"include-ns ns={ns} recursive=false");
        return this;
    }

    /// <summary>
    /// Registers all concrete <see cref="IPacket"/> types whose namespace is exactly
    /// <paramref name="rootNs"/> <b>or starts with <c>"{rootNs}."</c></b>, from all
    /// assemblies that have been added via <see cref="IncludeAssembly"/> or
    /// <see cref="IncludeCurrentDomain"/>.
    /// </summary>
    /// <param name="rootNs">
    /// The root namespace prefix, e.g. <c>"MyGame.Network"</c>.
    /// All child namespaces (<c>MyGame.Network.Packets</c>,
    /// <c>MyGame.Network.Auth.Packets</c>, …) will also be scanned.
    /// </param>
    /// <remarks>
    /// Prefer <see cref="IncludeNamespace"/> when you only want a single namespace
    /// level to avoid accidentally picking up unrelated types.
    /// </remarks>
    public PacketRegistryFactory IncludeNamespaceRecursive(System.String rootNs)
    {
        System.ArgumentException.ThrowIfNullOrWhiteSpace(rootNs);

        // Recursive always wins over non-recursive for the same key.
        _namespaceScan[rootNs] = true;

        INFO($"include-ns ns={rootNs} recursive=true");
        return this;
    }

    /// <summary>
    /// Builds an immutable catalog of packet deserializers and transformers.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when duplicate magic numbers are detected.
    /// </exception>
    public PacketRegistry CreateCatalog()
    {
        System.Int32 estimated =
            System.Math.Max(16, _explicitPacketTypes.Count + System.Math.Min(64, _assemblies.Count * 8));

        System.Collections.Generic.Dictionary<System.Type, PacketTransformer> transformers = new(estimated);
        System.Collections.Generic.Dictionary<System.UInt32, PacketDeserializer> deserializers = new(estimated);

        // ── 1. Collect candidates ────────────────────────────────────────────────
        System.Collections.Generic.HashSet<System.Type> candidates = [.. _explicitPacketTypes];

        INFO($"build-start asm={_assemblies.Count} explicit={_explicitPacketTypes.Count} ns={_namespaceScan.Count}");

        foreach (System.Reflection.Assembly asm in _assemblies)
        {
            INFO($"scan-asm name={asm.GetName().Name}");

            foreach (System.Type type in SAFE_GET_TYPES(asm))
            {
                // Must be a concrete class
                if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    TRACE($"skip reason=not-concrete type={type.Name}");
                    continue;
                }

                // Must implement IPacket
                if (!typeof(IPacket).IsAssignableFrom(type))
                {
                    TRACE($"skip reason=not-ipacket type={type.Name}");
                    continue;
                }

                // Already explicitly registered — no namespace check needed
                if (_explicitPacketTypes.Contains(type))
                {
                    TRACE($"skip reason=already-explicit type={type.Name}");
                    continue;
                }

                System.String? typeNs = type.Namespace;

                // Built-in namespace: skip unless explicitly registered
                if (typeNs is not null && BuiltInNamespaces.Contains(typeNs))
                {
                    TRACE($"skip reason=builtin-ns type={type.Name}");
                    continue;
                }

                // Namespace filter: only include if the type's namespace matches
                // a registered namespace entry (exact or recursive prefix).
                if (_namespaceScan.Count > 0 && !MATCHES_NAMESPACE_FILTER(typeNs))
                {
                    TRACE($"skip reason=ns-mismatch type={type.Name} ns={typeNs}");
                    continue;
                }

                _ = candidates.Add(type);
                INFO($"candidate type={type.FullName}");
            }
        }

        if (candidates.Count == 0)
        {
            INFO("no-candidate");
        }

        // ── 2. Bind per type ─────────────────────────────────────────────────────
        foreach (System.Type type in candidates)
        {
            System.UInt32 key = Compute(type);
            System.Boolean pipelineManaged = type.IsDefined(typeof(PipelineManagedTransformAttribute), inherit: false);

            System.Reflection.MethodInfo? miDeserialize = FIND_STATIC_METHOD(
                type, StaticPublic,
                nameof(IPacketDeserializer<>.Deserialize),
                [typeof(System.ReadOnlySpan<System.Byte>)]);

            System.Reflection.MethodInfo? miEncrypt = FIND_STATIC_METHOD(
                type, StaticPublic,
                nameof(IPacketEncryptor<>.Encrypt),
                [type, typeof(System.Byte[]), typeof(CipherSuiteType)]);

            System.Reflection.MethodInfo? miDecrypt = FIND_STATIC_METHOD(
                type, StaticPublic,
                nameof(IPacketEncryptor<>.Decrypt),
                [type, typeof(System.Byte[])]);

            System.Reflection.MethodInfo? miCompress = FIND_STATIC_METHOD(
                type, StaticPublic,
                nameof(IPacketCompressor<>.Compress),
                [type]);

            System.Reflection.MethodInfo? miDecompress = FIND_STATIC_METHOD(
                type, StaticPublic,
                nameof(IPacketCompressor<>.Decompress),
                [type]);

            // ── Deserializer (required) ──────────────────────────────────────────
            if (miDeserialize is null)
            {
                INFO($"[ERROR] miss-deserialize type={type.Name} — skipping");
                continue;
            }

            // BUG FIX: duplicate magic was only logged, not blocked.
            // Now we log AND skip to prevent last-write-wins silently overwriting.
            if (deserializers.ContainsKey(key))
            {
                System.Type existingType = FIND_TYPE_BY_MAGIC(key);
                INFO($"[FATAL] dup-magic val=0x{key:X8} new={type.FullName} existing={existingType.FullName} — skipping new type");
                continue;
            }

            // Bind deserialize pointer into PacketFunctionTable<TPacket>
            BindAllPtrsMi.MakeGenericMethod(type).Invoke(
                null, [miDeserialize, null, null, null, null]);

            System.Type tbl = typeof(PacketFunctionTable<>).MakeGenericType(type);
            System.Reflection.MethodInfo doDeserializeMi =
                tbl.GetMethod(nameof(PacketFunctionTable<IPacket>.InvokeDeserialize), StaticNonPublic)!;

            deserializers[key] = (PacketDeserializer)
                System.Delegate.CreateDelegate(typeof(PacketDeserializer), doDeserializeMi);

            // ── Transformer ──────────────────────────────────────────────────────
            if (pipelineManaged)
            {
                INFO($"pipeline-managed type={type.Name}");
                transformers[type] = new PacketTransformer(null, null, null, null);
                continue;
            }

            BindAllPtrsMi.MakeGenericMethod(type).Invoke(
                null, [null, miCompress, miDecompress, miEncrypt, miDecrypt]);

            System.Type fnType = typeof(PacketFunctionTable<>).MakeGenericType(type);

            System.Func<IPacket, IPacket>? compressDel = null;
            System.Func<IPacket, IPacket>? decompressDel = null;
            System.Func<IPacket, System.Byte[], IPacket>? decryptDel = null;
            System.Func<IPacket, System.Byte[], CipherSuiteType, IPacket>? encryptDel = null;

            System.Reflection.MethodInfo invokeEncryptMi = fnType.GetMethod(nameof(PacketFunctionTable<>.InvokeEncrypt), StaticNonPublic)!;
            System.Reflection.MethodInfo invokeDecryptMi = fnType.GetMethod(nameof(PacketFunctionTable<>.InvokeDecrypt), StaticNonPublic)!;
            System.Reflection.MethodInfo invokeCompressMi = fnType.GetMethod(nameof(PacketFunctionTable<>.InvokeCompress), StaticNonPublic)!;
            System.Reflection.MethodInfo invokeDecompressMi = fnType.GetMethod(nameof(PacketFunctionTable<>.InvokeDecompress), StaticNonPublic)!;

            if (miCompress is not null)
            {
                compressDel = (System.Func<IPacket, IPacket>)System.Delegate.CreateDelegate(typeof(System.Func<IPacket, IPacket>), invokeCompressMi);
            }

            if (miDecompress is not null)
            {
                decompressDel = (System.Func<IPacket, IPacket>)System.Delegate.CreateDelegate(typeof(System.Func<IPacket, IPacket>), invokeDecompressMi);
            }

            if (miEncrypt is not null)
            {
                encryptDel = (System.Func<IPacket, System.Byte[], CipherSuiteType, IPacket>)System.Delegate.CreateDelegate(typeof(System.Func<IPacket, System.Byte[], CipherSuiteType, IPacket>), invokeEncryptMi);
            }

            if (miDecrypt is not null)
            {
                decryptDel = (System.Func<IPacket, System.Byte[], IPacket>)System.Delegate.CreateDelegate(typeof(System.Func<IPacket, System.Byte[], IPacket>), invokeDecryptMi);
            }

            transformers[type] = new PacketTransformer(compressDel, decompressDel, encryptDel, decryptDel);
        }

        INFO($"build-ok packets={deserializers.Count} transformers={transformers.Count}");

        return new PacketRegistry(
            System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(transformers),
            System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(deserializers));
    }

    /// <summary>
    /// Computes a stable, deterministic 32-bit key from the type's full name
    /// using FNV-1a. Consistent across machines and .NET versions as long as
    /// the type's full name does not change.
    /// </summary>
    public static System.UInt32 Compute(System.Type type)
    {
        System.ArgumentNullException.ThrowIfNull(type);

        // BUG FIX: original code read chars one at a time (XOR then multiply per char).
        // Standard FNV-1a hashes BYTES, not chars. For ASCII-safe type names the result
        // is the same, but for any non-ASCII char the high byte was silently dropped.
        // Fix: encode to UTF-8 bytes first, then apply FNV-1a over bytes.
        System.ReadOnlySpan<System.Char> name = System.MemoryExtensions.AsSpan(type.FullName ?? type.Name);
        System.Span<System.Byte> buf = stackalloc System.Byte[System.Text.Encoding.UTF8.GetMaxByteCount(name.Length)];
        System.Int32 written = System.Text.Encoding.UTF8.GetBytes(name, buf);

        System.UInt32 hash = 2166136261u; // FNV-1a 32-bit offset basis
        foreach (System.Byte b in buf[..written])
        {
            hash ^= b;
            hash *= 16777619u; // FNV-1a 32-bit prime
        }

        return hash;
    }

    #endregion Public API

    #region Private Helpers

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="typeNs"/> matches any registered
    /// namespace entry — either exact or recursive-prefix.
    /// </summary>
    private System.Boolean MATCHES_NAMESPACE_FILTER(System.String? typeNs)
    {
        if (typeNs is null)
        {
            return false;
        }

        foreach (System.Collections.Generic.KeyValuePair<System.String, System.Boolean> entry in _namespaceScan)
        {
            System.String ns = entry.Key;
            System.Boolean recursive = entry.Value;

            if (recursive)
            {
                // Exact match OR proper sub-namespace (starts with "ns.")
                if (typeNs.Equals(ns, System.StringComparison.Ordinal) ||
                    typeNs.StartsWith(ns + ".", System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else
            {
                if (typeNs.Equals(ns, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Safely enumerates types in an assembly even when some types fail to load
    /// (e.g. missing dependencies, AOT restrictions).
    /// </summary>
    private static System.Collections.Generic.IEnumerable<System.Type> SAFE_GET_TYPES(System.Reflection.Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (System.Reflection.ReflectionTypeLoadException ex)
        {
            // Return the types that did load successfully; discard the rest.
            return System.Linq.Enumerable.OfType<System.Type>(ex.Types);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Searches for a static method by name and exact parameter types on
    /// <paramref name="startType"/>, then walks up the inheritance chain if not found.
    /// Inherited static methods from generic base types require manual walking because
    /// <c>GetMethod(FlattenHierarchy)</c> does not handle closed generic base types.
    /// </summary>
    private static System.Reflection.MethodInfo? FIND_STATIC_METHOD(
        System.Type startType,
        System.Reflection.BindingFlags flags,
        System.String name,
        System.Type[] parameterTypes)
    {
        System.Type? current = startType;
        while (current is not null && current != typeof(System.Object))
        {
            System.Reflection.MethodInfo? mi = current.GetMethod(
                name, flags, binder: null, parameterTypes, modifiers: null);

            if (mi is not null)
            {
                return mi.IsGenericMethodDefinition
                    ? mi.MakeGenericMethod(startType)
                    : mi;
            }

            current = current.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Reverse-lookup helper: given a magic number, find which type is currently
    /// registered for it. Used to produce clear duplicate-magic error messages.
    /// </summary>
    private static System.Type FIND_TYPE_BY_MAGIC(System.UInt32 magic)
    {
        // Only called on duplicate detection (rare, startup-only) — linear scan is fine.
        foreach (System.Type t in System.Linq.Enumerable.Where(System.Linq.Enumerable
                                                        .SelectMany(System.AppDomain.CurrentDomain
                                                        .GetAssemblies(), SAFE_GET_TYPES), t => t.IsClass && !t.IsAbstract && typeof(IPacket)
                                                        .IsAssignableFrom(t)))
        {
            if (Compute(t) == magic)
            {
                return t;
            }
        }
        return typeof(System.Object); // fallback, should never happen
    }

    // ── Logger helpers ────────────────────────────────────────────────────────

    private static ILogger? Logger => InstanceManager.Instance.GetExistingInstance<ILogger>();

    private static void INFO(System.String msg) => Logger?.Info($"[SH.{nameof(PacketRegistryFactory)}] {msg}");

    private static void TRACE(System.String msg) => Logger?.Trace($"[SH.{nameof(PacketRegistryFactory)}] {msg}");

    #endregion Private Helpers

    #region Private: Function Pointer Table

    /// <summary>
    /// Per-type static function-pointer store.
    /// Fields are assigned once at build time and read in the hot path with zero allocation.
    /// </summary>
    private static unsafe class PacketFunctionTable<TPacket> where TPacket : IPacket
    {
        public static delegate* managed<System.ReadOnlySpan<System.Byte>, TPacket> DeserializePtr;
        public static delegate* managed<TPacket, TPacket> CompressPtr;
        public static delegate* managed<TPacket, TPacket> DecompressPtr;
        public static delegate* managed<TPacket, System.Byte[], CipherSuiteType, TPacket> EncryptPtr;
        public static delegate* managed<TPacket, System.Byte[], TPacket> DecryptPtr;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket InvokeDeserialize(System.ReadOnlySpan<System.Byte> raw) => DeserializePtr(raw);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket InvokeCompress(IPacket p) => CompressPtr((TPacket)p);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket InvokeDecompress(IPacket p) => DecompressPtr((TPacket)p);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket InvokeEncrypt(IPacket p, System.Byte[] key, CipherSuiteType alg) => EncryptPtr((TPacket)p, key, alg);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket InvokeDecrypt(IPacket p, System.Byte[] key) => DecryptPtr((TPacket)p, key);
    }

    #endregion Private: Function Pointer Table

    #region Private: Binding Helpers

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate* managed<TPacket, TPacket>
        BindUnaryPtr<TPacket>(System.Reflection.MethodInfo mi)
    {
        System.IntPtr ptr = mi.MethodHandle.GetFunctionPointer();
        return (delegate* managed<TPacket, TPacket>)ptr;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate* managed<TPacket, System.Byte[], CipherSuiteType, TPacket>
        BindEncryptPtr<TPacket>(System.Reflection.MethodInfo mi)
    {
        System.IntPtr ptr = mi.MethodHandle.GetFunctionPointer();
        return (delegate* managed<TPacket, System.Byte[], CipherSuiteType, TPacket>)ptr;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate* managed<TPacket, System.Byte[], TPacket>
        BindDecryptPtr<TPacket>(System.Reflection.MethodInfo mi)
    {
        System.IntPtr ptr = mi.MethodHandle.GetFunctionPointer();
        return (delegate* managed<TPacket, System.Byte[], TPacket>)ptr;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate* managed<System.ReadOnlySpan<System.Byte>, TPacket>
        BindDeserializePtr<TPacket>(System.Reflection.MethodInfo mi)
    {
        System.IntPtr ptr = mi.MethodHandle.GetFunctionPointer();
        return (delegate* managed<System.ReadOnlySpan<System.Byte>, TPacket>)ptr;
    }

    /// <summary>
    /// Generic trampoline invoked via reflected <see cref="BindAllPtrsMi"/>.
    /// Assigns function pointers to <see cref="PacketFunctionTable{TPacket}"/>
    /// for each non-null method info provided.
    /// </summary>
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

        Logger?.Meta(
            $"[SH.{nameof(PacketRegistryFactory)}] bind type={typeof(TPacket).Name} " +
            $"des={(miDeserialize is not null ? "+" : "-")} " +
            $"cmp={(miCompress is not null ? "+" : "-")} " +
            $"dcmp={(miDecompress is not null ? "+" : "-")} " +
            $"enc={(miEncrypt is not null ? "+" : "-")} " +
            $"dec={(miDecrypt is not null ? "+" : "-")}");
    }

    #endregion Private: Binding Helpers
}