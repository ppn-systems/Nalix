// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.Codec.Memory;
using Nalix.Codec.Serialization;

namespace Nalix.Codec.DataFrames;

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

    private static readonly DiagnosticListener s_listener = new("Nalix.Codec");

    private const BindingFlags StaticPublic = BindingFlags.Public | BindingFlags.Static;
    private const BindingFlags StaticNonPublic = BindingFlags.NonPublic | BindingFlags.Static;

    private static readonly MethodInfo s_bindAllPtrsMi;

    // Built-in namespaces whose types are pre-registered in the default constructor.
    // These namespaces are skipped during scanning so the factory does not try to
    // register the same built-in packets twice.
    private static readonly FrozenSet<string> s_builtInNamespaces;

    #endregion Static: Defaults & Utilities

    #region Fields

    private readonly HashSet<Assembly> _assemblies = [];
    private readonly HashSet<Type> _explicitPacketTypes = [];
    private readonly HashSet<string> _exactNamespaces = new(StringComparer.Ordinal);
    private readonly HashSet<string> _recursiveNamespaces = new(StringComparer.Ordinal);

    #endregion Fields

    #region Constructors

    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicMethods, typeof(PacketRegistryFactory))]
    static PacketRegistryFactory()
    {
        s_builtInNamespaces = FrozenSet.ToFrozenSet(
        [
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
        // Control / handshake / session signal packets
        _ = this.RegisterPacket<Control>()
                .RegisterPacket<Handshake>()
                .RegisterPacket<SessionResume>()
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
        ArgumentNullException.ThrowIfNull(asm);

        int count = 0;
        foreach (Type? type in SAFE_GET_TYPES(asm))
        {
            if (type is null ||
                type.IsAbstract ||
                type.IsInterface ||
                type.IsGenericTypeDefinition ||
                !typeof(IPacket).IsAssignableFrom(type))
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
    /// Loads an assembly from <paramref name="assemblyPath"/> and registers all
    /// concrete packet types it contains.
    /// </summary>
    /// <param name="assemblyPath">Absolute or relative path to a .dll assembly.</param>
    /// <param name="requireAttribute">
    /// Only register classes with <see cref="PacketAttribute"/> when <see langword="true"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="assemblyPath"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the assembly file does not exist.
    /// </exception>
    public PacketRegistryFactory RegisterPacketAssembly(string assemblyPath, bool requireAttribute = false)
    {
        Assembly asm = LOAD_ASSEMBLY_FROM_PATH(assemblyPath);
        return this.RegisterAllPackets(asm, requireAttribute);
    }

    /// <summary>
    /// Adds an assembly to be scanned for packet types.
    /// Only types whose namespace is NOT in the built-in set will be considered.
    /// Keeping assembly selection separate lets the caller control the discovery
    /// scope without paying for a full AppDomain scan.
    /// </summary>
    public PacketRegistryFactory IncludeAssembly(Assembly? asm)
    {
        ArgumentNullException.ThrowIfNull(asm);

        bool added = _assemblies.Add(asm);
        TRACE(added ? $"include-asm name={asm.GetName().Name}" : $"include-asm-skip name={asm.GetName().Name}");

        return this;
    }

    /// <summary>
    /// Loads an assembly from <paramref name="assemblyPath"/> and adds it to the
    /// scanning scope used by namespace-based discovery.
    /// </summary>
    /// <param name="assemblyPath">Absolute or relative path to a .dll assembly.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="assemblyPath"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the assembly file does not exist.
    /// </exception>
    public PacketRegistryFactory IncludeAssembly(string assemblyPath)
    {
        Assembly asm = LOAD_ASSEMBLY_FROM_PATH(assemblyPath);
        return this.IncludeAssembly(asm);
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
    /// Registers all concrete packet types discovered from currently loaded assemblies.
    /// </summary>
    /// <param name="requireAttribute">
    /// Only register classes with <see cref="PacketAttribute"/> when <see langword="true"/>.
    /// </param>
    public PacketRegistryFactory RegisterCurrentDomainPackets(bool requireAttribute = false)
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            _ = this.RegisterAllPackets(asm, requireAttribute);
        }

        return this;
    }

    /// <summary>
    /// Registers all concrete <see cref="IPacket"/> types whose namespace exactly matches
    /// <paramref name="ns"/>, from all assemblies that have been added via
    /// <see cref="IncludeAssembly(Assembly)"/> or <see cref="IncludeCurrentDomain"/>.
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
        if (!_recursiveNamespaces.Contains(ns))
        {
            _ = _exactNamespaces.Add(ns);
        }

        TRACE($"include-ns ns={ns} recursive=false");
        return this;
    }

    /// <summary>
    /// Registers all concrete <see cref="IPacket"/> types whose namespace is exactly
    /// <paramref name="rootNs"/> <b>or starts with <c>"{rootNs}."</c></b>, from all
    /// assemblies that have been added via <see cref="IncludeAssembly(Assembly)"/> or
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

        _ = _exactNamespaces.Remove(rootNs);
        _ = _recursiveNamespaces.Add(rootNs);

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
        Dictionary<uint, PacketDeserializerInto<IPacket>> deserializersInto = new(estimated);
        Dictionary<uint, (Func<IPacket> Rent, Action<IPacket> Return)> poolOps = new(estimated);

        // ── 1. Collect candidates ────────────────────────────────────────────────
        HashSet<Type> candidates = [.. _explicitPacketTypes];

        TRACE($"build-start asm={_assemblies.Count} explicit={_explicitPacketTypes.Count} ns={_exactNamespaces.Count + _recursiveNamespaces.Count}");

        Dictionary<uint, Type> magicTypes = new(candidates.Count);

        foreach (Assembly asm in _assemblies)
        {
            TRACE($"scan-asm name={asm.GetName().Name}");

            foreach (Type type in SAFE_GET_TYPES(asm))
            {
                // Must be a concrete packet type (class or struct)
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
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
                if ((_exactNamespaces.Count != 0 || _recursiveNamespaces.Count != 0) &&
                    !this.MATCHES_NAMESPACE_FILTER(typeNs))
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
                    $"Packet type {type.FullName} does not implement the required static Deserialize(ReadOnlySpan<byte>) method.");

            if (deserializers.ContainsKey(key))
            {
                Type existingType = magicTypes[key];

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
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                throw new InternalErrorException(
                    $"Failed to bind deserialize pointer for packet type '{type.FullName}'.",
                    ex);
            }

            Type tbl = typeof(PacketFunctionTable<>).MakeGenericType(type);
            MethodInfo doDeserializeMi;
            MethodInfo doDeserializeIntoMi;
            try
            {
                doDeserializeMi = tbl.GetMethod(nameof(PacketFunctionTable<>.InvokeDeserialize), StaticNonPublic | StaticPublic)!;
                doDeserializeIntoMi = tbl.GetMethod(nameof(PacketFunctionTable<>.InvokeDeserializeInto), StaticNonPublic | StaticPublic)!;
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                throw new InternalErrorException(
                    $"Failed to resolve deserialize trampoline for packet type '{type.FullName}'.",
                    ex);
            }

            try
            {
                deserializers[key] = (PacketDeserializer)Delegate.CreateDelegate(typeof(PacketDeserializer), doDeserializeMi);
                magicTypes[key] = type;
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                throw new InternalErrorException(
                    $"Failed to create deserialize delegate for packet type '{type.FullName}'.",
                    ex);
            }
        }

        TRACE($"build-ok packets={deserializers.Count}");

        return new PacketRegistry(
            FrozenDictionary.ToFrozenDictionary(deserializers));
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

        const int MaxStackAlloc = 512;
        int maxBytes = Encoding.UTF8.GetMaxByteCount(name.Length);

        byte[]? rented = null;
        Span<byte> buf = maxBytes <= MaxStackAlloc
            ? stackalloc byte[maxBytes]
            : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(maxBytes));

        try
        {
            int written = Encoding.UTF8.GetBytes(name, buf);

            uint hash = 2166136261u; // FNV-1a 32-bit offset basis
            foreach (byte b in buf[..written])
            {
                hash ^= b;
                hash *= 16777619u; // FNV-1a 32-bit prime
            }

            return hash;
        }
        finally
        {
            if (rented is not null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }
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

        if (_exactNamespaces.Contains(typeNs))
        {
            return true;
        }

        foreach (string ns in _recursiveNamespaces)
        {
            if (typeNs.Equals(ns, StringComparison.Ordinal))
            {
                return true;
            }

            if (typeNs.Length > ns.Length &&
                typeNs.StartsWith(ns, StringComparison.Ordinal) &&
                typeNs[ns.Length] == '.')
            {
                return true;
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
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            return [];
        }
    }

    /// <summary>
    /// Resolves and loads an assembly from a file path while reusing an already loaded
    /// assembly with the same simple name when available.
    /// </summary>
    private static Assembly LOAD_ASSEMBLY_FROM_PATH(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        string fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The packet assembly file could not be found.", fullPath);
        }

        AssemblyName expected = AssemblyName.GetAssemblyName(fullPath);
        string? expectedName = expected.Name;
        if (!string.IsNullOrWhiteSpace(expectedName))
        {
            Assembly? alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(asm => string.Equals(asm.GetName().Name, expectedName, StringComparison.OrdinalIgnoreCase));

            if (alreadyLoaded is not null)
            {
                return alreadyLoaded;
            }
        }

        return Assembly.LoadFrom(fullPath);
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

    private static void TRACE(string msg)
    {
        if (s_listener.IsEnabled("registry"))
        {
            s_listener.Write("registry", new
            {
                Message = msg,
                Factory = nameof(PacketRegistryFactory),
                Timestamp = DateTime.UtcNow
            });
        }
    }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IPacket InvokeDeserializeInto(ReadOnlySpan<byte> raw, ref IPacket value)
        {
            // High-Performance Path: Try true zero-allocation rehydration if supported.
            if (value is TPacket existing)
            {
                // FormatterProvider is static and cached, so this is a fast call that does not allocate if the formatter already exists.
                IFormatter<TPacket> formatter = FormatterProvider.GetComplex<TPacket>();
                if (formatter is IFillableFormatter<TPacket> fillable)
                {
                    DataReader reader = new(raw);
                    fillable.Fill(ref reader, existing);
                    return existing;
                }
            }

            // Fallback Path: Standard deserialization. 
            // Since we are replacing the instance, we MUST dispose the old one if it was pooled
            // to prevent memory leaks.
            if (value is IDisposable disposable)
            {
                disposable.Dispose();
            }

            TPacket resolved = DeserializePtr(raw);

            // We update the provided ref and return the resolved instance.
            value = resolved;
            return resolved;
        }
    }

    #endregion Private: Function Pointer Table

    #region Private: Binding Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate* managed<ReadOnlySpan<byte>, TPacket> BIND_DESERIALIZE_PTR<TPacket>(MethodInfo mi)
    {
        nint ptr = mi.MethodHandle.GetFunctionPointer();
        return (delegate* managed<ReadOnlySpan<byte>, TPacket>)ptr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe delegate* managed<ReadOnlySpan<byte>, ref TPacket, TPacket> BIND_DESERIALIZE_INTO_PTR<TPacket>(MethodInfo mi)
    {
        nint ptr = mi.MethodHandle.GetFunctionPointer();
        return (delegate* managed<ReadOnlySpan<byte>, ref TPacket, TPacket>)ptr;
    }

    /// <summary>
    /// Generic trampoline invoked via reflected <see cref="s_bindAllPtrsMi"/>.
    /// Assigns the deserialize function pointer to <see cref="PacketFunctionTable{TPacket}"/>.
    /// </summary>
    private static unsafe void BIND_PTRS<TPacket>(
        MethodInfo miDeserialize) where TPacket : IPacket
    {
        PacketFunctionTable<TPacket>.DeserializePtr = BIND_DESERIALIZE_PTR<TPacket>(miDeserialize);
        TRACE($"[FW.{nameof(PacketRegistryFactory)}] bind type={typeof(TPacket).Name} des=+");
    }

    #endregion Private: Binding Helpers
}
