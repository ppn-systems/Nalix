// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Shared.Frames.Controls;
using Nalix.Shared.Frames.Text;

namespace Nalix.Shared.Frames;

/// <summary>
/// Builds an immutable <see cref="PacketRegistry"/> by scanning packet types and
/// binding their static deserialize functions with maximum performance.
/// </summary>
/// <remarks>
/// <para>
/// This implementation binds packet deserialize methods using <b>unsafe function
/// pointers</b> (<c>delegate*</c>) to eliminate delegate allocation and reduce
/// indirection in the hot path. Public-facing delegates are created only once at
/// build time as thin facades that jump directly to these function pointers.
/// </para>
/// <para>
/// Requirements for a packet type:
/// <list type="bullet">
///   <item>Implements <see cref="IPacket"/>.</item>
///   <item>Implements the static abstract members defined by
///         <see cref="IPacketDeserializer{TPacket}"/> on the concrete packet type.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class PacketRegistryFactory
{
    #region Static: Defaults & Utilities

    private const System.Reflection.BindingFlags StaticPublic = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
    private const System.Reflection.BindingFlags StaticNonPublic = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;

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
    private readonly System.Collections.Generic.Dictionary<System.String, System.Boolean> _namespaceScan = new(System.StringComparer.Ordinal);

    #endregion Fields

    #region Constructors

    static PacketRegistryFactory()
    {
        BuiltInNamespaces = System.Collections.Frozen.FrozenSet.ToFrozenSet(
            [
                typeof(Text256).Namespace!,
                typeof(Control).Namespace!
            ],
            System.StringComparer.Ordinal);

        BindAllPtrsMi = typeof(PacketRegistryFactory)
            .GetMethod(nameof(BIND_PTRS), StaticNonPublic)
            ?? throw new System.InvalidOperationException(
                $"Cannot locate private method '{nameof(BIND_PTRS)}' on {nameof(PacketRegistryFactory)}.");
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
    /// Builds an immutable catalog of packet deserializers.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when duplicate magic numbers are detected.
    /// </exception>
    public PacketRegistry CreateCatalog()
    {
        System.Int32 estimated =
            System.Math.Max(16, _explicitPacketTypes.Count + System.Math.Min(64, _assemblies.Count * 8));

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

            System.Reflection.MethodInfo? miDeserialize = FIND_STATIC_METHOD(
                type, StaticPublic,
                nameof(IPacketDeserializer<>.Deserialize),
                [typeof(System.ReadOnlySpan<System.Byte>)]);

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
            try
            {
                BindAllPtrsMi.MakeGenericMethod(type).Invoke(null, [miDeserialize]);
            }
            catch (System.Exception ex)
            {
                INFO($"bind-deserialize-fail type={type.Name} err={ex.Message}");
                continue;
            }

            System.Type tbl = typeof(PacketFunctionTable<>).MakeGenericType(type);
            System.Reflection.MethodInfo doDeserializeMi;
            try
            {
                doDeserializeMi = tbl.GetMethod(nameof(PacketFunctionTable<>.InvokeDeserialize), StaticNonPublic | StaticPublic)!;
            }
            catch (System.Exception ex)
            {
                INFO($"get-method-fail type={type.Name} method=InvokeDeserialize err={ex.Message}");
                continue;
            }

            try
            {
                deserializers[key] = (PacketDeserializer)System.Delegate.CreateDelegate(typeof(PacketDeserializer), doDeserializeMi);
            }
            catch (System.Exception ex)
            {
                INFO($"delegate-create-fail type={type.Name} err={ex.Message}");
                continue;
            }
        }

        INFO($"build-ok packets={deserializers.Count}");

        return new PacketRegistry(
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

    private static ILogger? Logging => InstanceManager.Instance.GetExistingInstance<ILogger>();

    private static void INFO(System.String msg) => Logging?.Info($"[SH.{nameof(PacketRegistryFactory)}] {msg}");

    private static void TRACE(System.String msg) => Logging?.Trace($"[SH.{nameof(PacketRegistryFactory)}] {msg}");

    #endregion Private Helpers

    #region Private: Function Pointer Table

    /// <summary>
    /// Per-type static function-pointer store.
    /// Fields are assigned once at build time and read in the hot path with zero allocation.
    /// </summary>
    private static unsafe class PacketFunctionTable<TPacket> where TPacket : IPacket
    {
        public static delegate* managed<System.ReadOnlySpan<System.Byte>, TPacket> DeserializePtr;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static IPacket InvokeDeserialize(System.ReadOnlySpan<System.Byte> raw) => DeserializePtr(raw);
    }

    #endregion Private: Function Pointer Table

    #region Private: Binding Helpers

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate* managed<System.ReadOnlySpan<System.Byte>, TPacket> BIND_DESERIALIZE_PTR<TPacket>(System.Reflection.MethodInfo mi)
    {
        System.IntPtr ptr = mi.MethodHandle.GetFunctionPointer();
        return (delegate* managed<System.ReadOnlySpan<System.Byte>, TPacket>)ptr;
    }

    /// <summary>
    /// Generic trampoline invoked via reflected <see cref="BindAllPtrsMi"/>.
    /// Assigns the deserialize function pointer to <see cref="PacketFunctionTable{TPacket}"/>.
    /// </summary>
    private static unsafe void BIND_PTRS<TPacket>(
        System.Reflection.MethodInfo miDeserialize) where TPacket : IPacket
    {
        PacketFunctionTable<TPacket>.DeserializePtr = BIND_DESERIALIZE_PTR<TPacket>(miDeserialize);

        Logging?.Meta($"[SH.{nameof(PacketRegistryFactory)}] bind type={typeof(TPacket).Name} des=+");
    }

    #endregion Private: Binding Helpers
}