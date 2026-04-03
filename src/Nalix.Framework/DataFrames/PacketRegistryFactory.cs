// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.DataFrames.TextFrames;
using Nalix.Framework.Injection;

namespace Nalix.Framework.DataFrames;

/// <summary>
/// Builds an immutable <see cref="PacketRegistry"/> by scanning packet types and
/// binding their static deserialize functions with maximum performance.
/// The factory separates discovery from registration so packet metadata can be
/// assembled once and reused by the runtime registry.
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

    private const BindingFlags StaticPublic = BindingFlags.Public | BindingFlags.Static;
    private const BindingFlags StaticNonPublic = BindingFlags.NonPublic | BindingFlags.Static;

    private static readonly MethodInfo s_bindAllPtrsMi;

    // Built-in namespaces whose types are pre-registered in the default constructor.
    // These namespaces are skipped during scanning so the factory does not try to
    // register the same built-in packets twice.
    private static readonly FrozenSet<string> s_builtInNamespaces;

    #endregion Static: Defaults & Utilities

    #region Fields

    private readonly HashSet<Type> _explicitPacketTypes = [];
    private readonly HashSet<Assembly> _assemblies = [];

    // Namespace filter table:
    //   key   = namespace string
    //   value = true when the namespace should match recursively
    // Exact matches are cheaper, but recursive matches are useful when a whole
    // packet family lives under a shared root namespace.
    private readonly Dictionary<string, bool> _namespaceScan = new(StringComparer.Ordinal);

    #endregion Fields

    #region Constructors

    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicMethods, typeof(PacketRegistryFactory))]
    static PacketRegistryFactory()
    {
        s_builtInNamespaces = FrozenSet.ToFrozenSet(
        [
            typeof(Text256).Namespace!,
        typeof(Control).Namespace!
        ],
        StringComparer.Ordinal);

        s_bindAllPtrsMi = typeof(PacketRegistryFactory).GetMethod(nameof(BIND_PTRS), StaticNonPublic)
            ?? throw new InternalErrorException($"Missing method: {nameof(PacketRegistryFactory)}.{nameof(BIND_PTRS)} (Static, NonPublic).");
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

    #region Public API

    /// <summary>
    /// Registers a concrete packet type explicitly.
    /// Explicit registrations are useful when packet types are known ahead of
    /// time and should always be present even if assembly scanning is disabled.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <typeparamref name="TPacket"/> does not expose a stable packet identity at registration time.</exception>
    public PacketRegistryFactory RegisterPacket<TPacket>() where TPacket : IPacket
    {
        Type t = typeof(TPacket);

        if (!_explicitPacketTypes.Add(t))
        {
            TRACE($"register-packet-skip type={t.FullName} reason=already-registered");
        }

        return this;
    }

    /// <summary>
    /// Register all packets that implement <see cref="IPacket"/> in the assembly.
    /// If <paramref name="requireAttribute"/> is true, only register classes that have <see cref="PacketAttribute"/>.
    /// This is the broad discovery path used when the caller wants the factory to
    /// harvest packet types automatically from a plugin assembly.
    /// </summary>
    /// <param name="asm">Assembly to scan packets.</param>
    /// <param name="requireAttribute">Only register classes with the attribute if true; register all if false.</param>
    public PacketRegistryFactory RegisterAllPackets(Assembly? asm, bool requireAttribute = false)
    {
        if (asm is null) { TRACE("include-asm-null"); return this; }

        int count = 0;
        foreach (Type? type in SAFE_GET_TYPES(asm))
        {
            if (type is null || !type.IsClass || type.IsAbstract || !typeof(IPacket).IsAssignableFrom(type))
            {
                continue;
            }

            if (requireAttribute && type.GetCustomAttributes(typeof(PacketAttribute), inherit: false).Length == 0)
            {
                continue;
            }

            _ = _explicitPacketTypes.Add(type);
            TRACE($"register-packet type={type.FullName} attr={requireAttribute}");

            count++;
        }

        TRACE($"register-packets-complete from asm={asm.GetName().Name} packets={count}");
        return this;
    }

    /// <summary>
    /// Adds an assembly to be scanned for packet types.
    /// Only types whose namespace is NOT in the built-in set will be considered.
    /// Keeping assembly selection separate lets the caller control the discovery
    /// scope without paying for a full AppDomain scan.
    /// </summary>
    public PacketRegistryFactory IncludeAssembly(Assembly? asm)
    {
        if (asm is null) { TRACE("include-asm-null"); return this; }

        bool added = _assemblies.Add(asm);
        TRACE(added ? $"include-asm name={asm.GetName().Name}" : $"include-asm-skip name={asm.GetName().Name}");

        return this;
    }

    /// <summary>
    /// Scans all loaded assemblies in the current <see cref="AppDomain"/>
    /// for packet types.
    /// This is the widest discovery mode and is usually only appropriate when
    /// the caller wants to auto-register everything available at runtime.
    /// </summary>
    public PacketRegistryFactory IncludeCurrentDomain()
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            _ = this.IncludeAssembly(asm);
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
    public PacketRegistryFactory IncludeNamespace(string ns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ns);

        // If already registered as recursive, keep recursive (superset).
        if (!_namespaceScan.TryGetValue(ns, out bool existing) || !existing)
        {
            _namespaceScan[ns] = false;
        }

        TRACE($"include-ns ns={ns} recursive=false");
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
    public PacketRegistryFactory IncludeNamespaceRecursive(string rootNs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootNs);

        // Recursive always wins over non-recursive for the same key.
        _namespaceScan[rootNs] = true;

        TRACE($"include-ns ns={rootNs} recursive=true");
        return this;
    }

    /// <summary>
    /// Builds an immutable catalog of packet deserializers.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when duplicate magic numbers are detected or when required deserialize bindings cannot be created consistently.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a packet type resolves to an invalid full name during magic computation.
    /// </exception>
    public PacketRegistry CreateCatalog()
    {
        int estimated =
            Math.Max(16, _explicitPacketTypes.Count + Math.Min(64, _assemblies.Count * 8));

        Dictionary<uint, PacketDeserializer> deserializers = new(estimated);

        // ── 1. Collect candidates ────────────────────────────────────────────────
        HashSet<Type> candidates = [.. _explicitPacketTypes];

        TRACE($"build-start asm={_assemblies.Count} explicit={_explicitPacketTypes.Count} ns={_namespaceScan.Count}");

        foreach (Assembly asm in _assemblies)
        {
            TRACE($"scan-asm name={asm.GetName().Name}");

            foreach (Type type in SAFE_GET_TYPES(asm))
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

                string? typeNs = type.Namespace;

                // Built-in namespace: skip unless explicitly registered
                if (typeNs is not null && s_builtInNamespaces.Contains(typeNs))
                {
                    TRACE($"skip reason=builtin-ns type={type.Name}");
                    continue;
                }

                // Namespace filter: only include if the type's namespace matches
                // a registered namespace entry (exact or recursive prefix).
                if (_namespaceScan.Count > 0 && !this.MATCHES_NAMESPACE_FILTER(typeNs))
                {
                    TRACE($"skip reason=ns-mismatch type={type.Name} ns={typeNs}");
                    continue;
                }

                _ = candidates.Add(type);
                TRACE($"candidate type={type.FullName}");
            }
        }

        if (candidates.Count == 0)
        {
            throw new InternalErrorException("No packet types found for registration. Please check your assembly/namespace configuration.");
        }

        // ── 2. Bind per type ─────────────────────────────────────────────────────
        foreach (Type type in candidates)
        {
            uint key = Compute(type);

            MethodInfo? miDeserialize = FIND_STATIC_METHOD(
                type, StaticPublic,
                nameof(IPacketDeserializer<>.Deserialize),
                [typeof(ReadOnlySpan<byte>)]) ?? throw new InternalErrorException(
                    $"Packet type {type.FullName} does not implement " +
                    $"the required static Deserialize(ReadOnlySpan<byte>) method.");
            if (deserializers.TryGetValue(key, out PacketDeserializer? existing))
            {
                Type existingType = FIND_TYPE_BY_MAGIC(key);

                throw new InternalErrorException(
                    $"[PacketRegistryFactory] Hash collision detected!\n" +
                    $"Magic: 0x{key:X8}\n" +
                    $"Type A: {existingType.FullName}\n" +
                    $"Type B: {type.FullName}\n" +
                    $"Hint: consider changing namespace or switching to 64-bit hash."
                );
            }

            // Bind deserialize pointer into PacketFunctionTable<TPacket>
            try
            {
                _ = s_bindAllPtrsMi.MakeGenericMethod(type).Invoke(null, [miDeserialize]);
            }
            catch (Exception ex)
            {
                TRACE($"bind-deserialize-fail type={type.Name} err={ex.Message}");
                continue;
            }

            Type tbl = typeof(PacketFunctionTable<>).MakeGenericType(type);
            MethodInfo doDeserializeMi;
            try
            {
                doDeserializeMi = tbl.GetMethod(nameof(PacketFunctionTable<>.InvokeDeserialize), StaticNonPublic | StaticPublic)!;
            }
            catch (Exception ex)
            {
                TRACE($"get-method-fail type={type.Name} method=InvokeDeserialize err={ex.Message}");
                continue;
            }

            try
            {
                deserializers[key] = (PacketDeserializer)Delegate.CreateDelegate(typeof(PacketDeserializer), doDeserializeMi);
            }
            catch (Exception ex)
            {
                TRACE($"delegate-create-fail type={type.Name} err={ex.Message}");
                continue;
            }
        }

        TRACE($"build-ok packets={deserializers.Count}");

        return new PacketRegistry(FrozenDictionary.ToFrozenDictionary(deserializers));
    }

    /// <summary>
    /// Computes a stable, deterministic 32-bit key from the type's full name
    /// using FNV-1a. Consistent across machines and .NET versions as long as
    /// the type's full name does not change.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
    public static uint Compute(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        // Standard FNV-1a hashes BYTES, not chars. For ASCII-safe type names the result
        // is the same, but for any non-ASCII char the high byte was silently dropped.
        ReadOnlySpan<char> name = MemoryExtensions.AsSpan(type.FullName ?? type.Name);
        Span<byte> buf = stackalloc byte[Encoding.UTF8.GetMaxByteCount(name.Length)];

        int written = Encoding.UTF8.GetBytes(name, buf);

        uint hash = 2166136261u; // FNV-1a 32-bit offset basis
        foreach (byte b in buf[..written])
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
    private bool MATCHES_NAMESPACE_FILTER(string? typeNs)
    {
        if (typeNs is null)
        {
            return false;
        }

        foreach (KeyValuePair<string, bool> entry in _namespaceScan)
        {
            string ns = entry.Key;
            bool recursive = entry.Value;

            if (recursive)
            {
                // Exact match OR proper sub-namespace (starts with "ns.")
                if (typeNs.Equals(ns, StringComparison.Ordinal) ||
                    typeNs.StartsWith(ns + ".", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else
            {
                if (typeNs.Equals(ns, StringComparison.Ordinal))
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
    [RequiresUnreferencedCode("Calls System.Reflection.Assembly.GetTypes()")]
    private static IEnumerable<Type> SAFE_GET_TYPES(Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Return the types that did load successfully; discard the rest.
            return Enumerable.OfType<Type>(ex.Types);
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
    [RequiresDynamicCode("Calls System.Reflection.MethodInfo.MakeGenericMethod(params Type[])")]
    private static MethodInfo? FIND_STATIC_METHOD(
        Type startType,
        BindingFlags flags,
        string name,
        Type[] parameterTypes)
    {
        Type? current = startType;
        while (current is not null && current != typeof(object))
        {
            MethodInfo? mi = current.GetMethod(
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
    [RequiresUnreferencedCode("Calls Nalix.Framework.DataFrames.PacketRegistryFactory.SAFE_GET_TYPES(Assembly)")]
    private static Type FIND_TYPE_BY_MAGIC(uint magic)
    {
        // Only called on duplicate detection (rare, startup-only) — linear scan is fine.
        foreach (Type t in Enumerable.Where(Enumerable
                                     .SelectMany(AppDomain.CurrentDomain
                                     .GetAssemblies(), SAFE_GET_TYPES), t => t.IsClass && !t.IsAbstract && typeof(IPacket)
                                     .IsAssignableFrom(t)))
        {
            if (Compute(t) == magic)
            {
                return t;
            }
        }
        return typeof(object); // fallback, should never happen
    }

    // ── Logger helpers ────────────────────────────────────────────────────────

    private static ILogger? Logging => InstanceManager.Instance.GetExistingInstance<ILogger>();

    private static void TRACE(string msg) => Logging?.Trace($"[SH.{nameof(PacketRegistryFactory)}] {msg}");

    #endregion Private Helpers

    #region Private: Function Pointer Table

    /// <summary>
    /// Per-type static function-pointer store.
    /// Fields are assigned once at build time and read in the hot path with zero allocation.
    /// </summary>
    private static unsafe class PacketFunctionTable<TPacket> where TPacket : IPacket
    {
        public static delegate* managed<ReadOnlySpan<byte>, TPacket> DeserializePtr;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IPacket InvokeDeserialize(ReadOnlySpan<byte> raw) => DeserializePtr(raw);
    }

    #endregion Private: Function Pointer Table

    #region Private: Binding Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate* managed<ReadOnlySpan<byte>, TPacket> BIND_DESERIALIZE_PTR<TPacket>(MethodInfo mi)
    {
        nint ptr = mi.MethodHandle.GetFunctionPointer();
        return (delegate* managed<ReadOnlySpan<byte>, TPacket>)ptr;
    }

    /// <summary>
    /// Generic trampoline invoked via reflected <see cref="s_bindAllPtrsMi"/>.
    /// Assigns the deserialize function pointer to <see cref="PacketFunctionTable{TPacket}"/>.
    /// </summary>
    private static unsafe void BIND_PTRS<TPacket>(
        MethodInfo miDeserialize) where TPacket : IPacket
    {
        PacketFunctionTable<TPacket>.DeserializePtr = BIND_DESERIALIZE_PTR<TPacket>(miDeserialize);

        Logging?.Trace($"[SH.{nameof(PacketRegistryFactory)}] bind type={typeof(TPacket).Name} des=+");
    }

    #endregion Private: Binding Helpers
}
