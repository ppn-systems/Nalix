// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Internal.Results;

namespace Nalix.Runtime.Internal.Compilation;

/// <summary>
/// High-performance controller scanner with caching and zero-allocation lookups.
/// Uses compiled expression trees for maximum dispatch performance.
/// </summary>
/// <typeparam name="TController">The controller type to scan.</typeparam>
/// <typeparam name="TPacket">The packet type handled by this controller.</typeparam>
[DebuggerNonUserCode]
[SkipLocalsInit]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class PacketHandlerCompiler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TController, TPacket>()
    where TController : class where TPacket : IPacket
{
    #region Fields

    /// <summary>
    /// Caches attribute lookups per method for performance.
    /// </summary>
    private static readonly ConcurrentDictionary<MethodInfo, PacketMetadata> s_attributeCache = new();

    /// <summary>
    /// Caches compiled method delegates for each controller type to eliminate reflection.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, FrozenDictionary<ushort, PacketHandlerDescriptor<TPacket>>> s_compiledMethodCache = new();

    #endregion Fields

    #region APIs

    /// <summary>
    /// Scans the controller and returns an array of packet handler delegates.
    /// </summary>
    /// <param name="factory">A factory method that creates a controller instance.</param>
    /// <returns>An array of compiled packet handler delegates.</returns>
    /// <exception cref="InternalErrorException"></exception>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static PacketHandler<TPacket>[] CompileHandlers(Func<TController> factory)
    {
        Type controllerType = typeof(TController);

        // Ensure controller has [PacketController] attribute
        PacketControllerAttribute controllerAttr = CustomAttributeExtensions.GetCustomAttribute<PacketControllerAttribute>(controllerType)
            ?? throw new InternalErrorException($"Controller '{controllerType.Name}' is missing the [PacketController] attribute.");

        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        if (logger != null && logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug($"[RT.{nameof(PacketHandlerCompiler<,>)}:{nameof(CompileHandlers)}] scan controller={controllerType.Name}");
        }

        // Reuse cached method metadata when possible; otherwise compile once and
        // freeze the result so dispatch stays allocation-free at runtime.
        FrozenDictionary<ushort, PacketHandlerDescriptor<TPacket>> compiledMethods = COMPILE_CONTROLLER_HANDLERS(controllerType);

        // Create one controller instance up front and reuse it for every handler.
        TController controllerInstance = factory();

        // CreateCatalog delegate descriptors
        PacketHandler<TPacket>[] descriptors = new PacketHandler<TPacket>[compiledMethods.Count];
        int index = 0;

        foreach ((ushort opCode, PacketHandlerDescriptor<TPacket> compiledMethod) in compiledMethods)
        {
            PacketMetadata attributes = GET_PACKET_METADATA(compiledMethod.MethodInfo);

            descriptors[index++] = new PacketHandler<TPacket>(
                opCode,
                attributes,
                controllerInstance,
                compiledMethod.MethodInfo,
                compiledMethod.ReturnType,
                compiledMethod.CompiledInvoker,
                expectedPacketType: compiledMethod.ExpectedPacketType,
                returnHandler: ReturnTypeHandlerFactory<TPacket>.ResolveHandler(compiledMethod.ReturnType));
        }

        string firstOps = string.Join(",", Enumerable
                                              .Select(Enumerable
                                              .Take(compiledMethods.Keys, 6), o => $"0x{o:X4}"));

        if (logger != null && logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug($"[RT.{nameof(PacketHandlerCompiler<,>)}:{nameof(CompileHandlers)}] " +
                                   $"found count={compiledMethods.Count} controller={controllerType.FullName} ops=[{firstOps}{(compiledMethods.Count > 6 ? ",..." : string.Empty)}]");
        }

        return descriptors;
    }

    #endregion APIs

    #region Private Methods

    /// <summary>
    /// Describes the recognized parameter signature of a handler method.
    /// </summary>
    private enum SignatureKind
    {
        /// <summary>
        /// (TPacket, IConnection)
        /// </summary>
        LegacyNoToken = 0,

        /// <summary>
        /// (TPacket, IConnection, CancellationToken)
        /// </summary>
        LegacyWithToken = 1,

        /// <summary>
        /// (PacketContext&lt;TPacket&gt;)
        /// </summary>
        ContextOnly = 2,

        /// <summary>
        /// (PacketContext&lt;TPacket&gt;, CancellationToken)
        /// </summary>
        ContextWithToken = 3,

        /// <summary>
        /// (TConcretePacket, IConnection) where TConcretePacket : IPacket and TConcretePacket != TPacket.
        /// The dispatcher will perform a runtime type-check and cast before invoking.
        /// </summary>
        LegacyConcreteNoToken = 4,

        /// <summary>
        /// (TConcretePacket, IConnection, CancellationToken) where TConcretePacket : IPacket and TConcretePacket != TPacket.
        /// The dispatcher will perform a runtime type-check and cast before invoking.
        /// </summary>
        LegacyConcreteWithToken = 5,

        /// <summary>
        /// (ReadOnlyMemory&lt;byte&gt;, IConnection)
        /// Extracts raw memory payload from RawMemoryPacket.
        /// </summary>
        MemoryNoToken = 6,

        /// <summary>
        /// (ReadOnlyMemory&lt;byte&gt;, IConnection, CancellationToken)
        /// Extracts raw memory payload from RawMemoryPacket.
        /// </summary>
        MemoryWithToken = 7,
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static FrozenDictionary<ushort, PacketHandlerDescriptor<TPacket>> COMPILE_CONTROLLER_HANDLERS(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type x03)
    {
        // Get methods with [PacketOpcode] attribute
        MethodInfo[] methodInfos = Enumerable.ToArray(
            Enumerable.Where(
                x03.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.Static
                ),
                m => CustomAttributeExtensions.GetCustomAttribute<PacketOpcodeAttribute>(m) is not null));

        ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
        if (methodInfos.Length == 0)
        {
            if (logger != null && logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug($"[RT.{nameof(PacketHandlerCompiler<,>)}:Internal] no-method controller={x03.Name}");
            }
        }

        if (logger != null && logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug($"[RT.{nameof(PacketHandlerCompiler<,>)}:Internal] compile count={methodInfos.Length} controller={x03.Name}");
        }

        return s_compiledMethodCache.GetOrAdd(x03, static (_, methods) =>
        {
            Dictionary<ushort, PacketHandlerDescriptor<TPacket>> compiled = new(methods.Length);

            foreach (MethodInfo method in methods)
            {
                PacketOpcodeAttribute? opcodeAttr = CustomAttributeExtensions
                    .GetCustomAttribute<PacketOpcodeAttribute>(method);

                if (opcodeAttr is null)
                {
                    continue;
                }

                if (compiled.ContainsKey(opcodeAttr.OpCode))
                {
                    string x01 = FORMAT_HANDLER_INFO(method.DeclaringType?.Name ?? "None", opcodeAttr.OpCode, method, method.ReturnType);

                    ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
                    if (logger != null && logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning($"[RT.{nameof(PacketHandlerCompiler<,>)}:Internal] dup-opcode {x01}");
                    }

                    continue;
                }

                try
                {
                    compiled[opcodeAttr.OpCode] = COMPILE_HANDLER_METHOD(method);

                    string x01 = FORMAT_HANDLER_INFO(method.DeclaringType?.Name ?? "None", opcodeAttr.OpCode, method, method.ReturnType);

                    ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
                    if (logger != null && logger.IsEnabled(LogLevel.Trace))
                    {
                        logger.LogTrace($"[RT.{nameof(PacketHandlerCompiler<,>)}:Internal] compiled {x01}");
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    string x01 = FORMAT_HANDLER_INFO(method.DeclaringType?.Name ?? "None", opcodeAttr.OpCode, method, method.ReturnType);

                    ILogger? logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
                    if (logger != null && logger.IsEnabled(LogLevel.Error))
                    {
                        logger.LogError(ex, $"[RT.{nameof(PacketHandlerCompiler<,>)}:Internal] failed-compile {x01}");
                    }

                    throw; // BUG-78: Fail-fast instead of continuing with incomplete handlers
                }
            }

            return FrozenDictionary.ToFrozenDictionary(compiled);
        }, methodInfos);
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static PacketHandlerDescriptor<TPacket> COMPILE_HANDLER_METHOD(MethodInfo x22)
    {
        // Shared expression nodes — always built regardless of signature kind.
        // x00 = boxed controller instance
        // x01 = PacketContext<TPacket> (the single source-of-truth arg the
        //       compiled invoker always receives from ExecuteHandlerAsync)
        // x02..x04 = property reads off x01
        ParameterExpression x00 =
            Expression.Parameter(typeof(object), "instance");

        ParameterExpression x01 =
            Expression.Parameter(typeof(PacketContext<TPacket>), "context");

        Type contextType = typeof(PacketContext<TPacket>);
        PropertyInfo packetProperty = GET_REQUIRED_PROPERTY(contextType, nameof(PacketContext<>.Packet));
        PropertyInfo connectionProperty = GET_REQUIRED_PROPERTY(contextType, nameof(PacketContext<>.Connection));
        PropertyInfo cancellationTokenProperty = GET_REQUIRED_PROPERTY(contextType, nameof(PacketContext<>.CancellationToken));

        MemberExpression x02 =
            Expression.Property(x01, packetProperty);

        MemberExpression x03 =
            Expression.Property(x01, connectionProperty);

        MemberExpression x04 =
            Expression.Property(x01, cancellationTokenProperty);

        // Detect which of the 4 supported signatures this method uses.
        // Supported forms:
        //   Legacy  (a) (TPacket, IConnection)
        //   Legacy  (b) (TPacket, IConnection, CancellationToken)
        //   New     (c) (PacketContext<TPacket>)
        //   New     (d) (PacketContext<TPacket>, CancellationToken)
        ParameterInfo[] parms = x22.GetParameters();

        SignatureKind kind = RESOLVE_SIGNATURE_KIND(x22, parms);

        // Context-style with a DIFFERENT concrete PacketContext<T>
        //
        // When TPacket = IPacket but the handler declares PacketContext<Handshake>,
        // Expression.Convert cannot bridge PacketContext<IPacket> to PacketContext<Handshake>
        // because generic classes are invariant — no coercion operator exists between them.
        //
        // Solution: skip the expression-tree path for this case and use a reflection-based
        // bridge instead. MethodInfo.Invoke boxes arguments to object and performs the
        // assignability check at runtime via CLR rules, accepting PacketContext<Handshake>
        // without any explicit cast.
        Type? expectedPacketType = kind switch
        {
            SignatureKind.MemoryNoToken or SignatureKind.MemoryWithToken => typeof(MemoryPacket),
            SignatureKind.LegacyConcreteNoToken or SignatureKind.LegacyConcreteWithToken => parms[0].ParameterType,
            SignatureKind.ContextOnly or SignatureKind.ContextWithToken when parms[0].ParameterType.IsGenericType => parms[0].ParameterType.GetGenericArguments()[0] == typeof(TPacket) ? null : parms[0].ParameterType.GetGenericArguments()[0],
            SignatureKind.LegacyNoToken or SignatureKind.LegacyWithToken or _ => null
        };

        bool needsContextBridge =
            (kind is SignatureKind.ContextOnly or SignatureKind.ContextWithToken)
            && parms[0].ParameterType != typeof(PacketContext<TPacket>)
            && parms[0].ParameterType != typeof(IPacketContext<TPacket>);

        Func<object, PacketContext<TPacket>, ValueTask<object>> x20;

        if (needsContextBridge)
        {
            x20 = BUILD_CONTEXT_BRIDGE_INVOKER(x22, parms, kind);
        }
        else
        {
            Func<object, PacketContext<TPacket>, object> x12;

            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                // Normal expression-tree path — types match exactly.
                Expression[] x09 = BUILD_ARG_EXPRESSIONS(kind, parms, x01, x02, x03, x04);

                Expression x10 = x22.IsStatic
                    ? Expression.Call(x22, x09)
                    : Expression.Call(
                        Expression.Convert(x00, x22.DeclaringType
                            ?? throw new InternalErrorException($"Handler method '{x22.Name}' is missing a declaring type.")), x22, x09);

                Expression x11 = x22.ReturnType == typeof(void)
                    ? System.Linq.Expressions.Expression.Block(x10, System.Linq.Expressions.Expression.Constant(null, typeof(object)))
                    : System.Linq.Expressions.Expression.Convert(x10, typeof(object));

                x12 = Expression
                        .Lambda<Func<object, PacketContext<TPacket>, object>>(x11, x00, x01)
                        .Compile();
            }
            else
            {
                // AOT fallback — build invoke args at call-time from context fields.
                x12 = BUILD_AOT_INVOKER(x22, parms, kind);
            }

            x20 = WRAP_RETURN_TYPE(x12, x22.ReturnType);
        }

        return new PacketHandlerDescriptor<TPacket>(x22, x22.ReturnType, expectedPacketType, x20);
    }

    /// <summary>
    /// Determines the <see cref="SignatureKind"/> of a handler method.
    /// Throws <see cref="InternalErrorException"/> for unrecognised signatures.
    /// </summary>
    /// <param name="method"></param>
    /// <param name="parms"></param>
    /// <exception cref="InternalErrorException"></exception>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SignatureKind RESOLVE_SIGNATURE_KIND(MethodInfo method, ParameterInfo[] parms)
    {
        // ---- new-style: first param is PacketContext<T> for any T : IPacket ----
        // Use generic-definition comparison instead of exact-type equality so that
        // PacketContext<LoginPacket> is recognised when TPacket = IPacket.
        if (parms.Length >= 1 && IS_PACKET_CONTEXT_TYPE(parms[0].ParameterType))
        {
            // When the declared context type argument differs from TPacket, the
            // needsContextBridge path in CompileHandlerMethod will handle the
            // coercion via reflection — no throw here.
            if (parms.Length == 1)
            {
                return SignatureKind.ContextOnly;
            }

            return parms.Length == 2 && parms[1].ParameterType == typeof(CancellationToken)
                ? SignatureKind.ContextWithToken
                : throw new InternalErrorException(
                        $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
                        "when the first parameter is PacketContext<T>, " +
                        "the only valid second parameter is CancellationToken. " +
                        $"Found {parms.Length} parameter(s).");
        }

        // ---- new-style: raw memory payload ----
        if (parms.Length >= 1 && parms[0].ParameterType == typeof(ReadOnlyMemory<byte>))
        {
            if (parms.Length < 2 || !typeof(IConnection).IsAssignableFrom(parms[1].ParameterType))
            {
                throw new InternalErrorException(
                    $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
                    "raw memory signature requires (ReadOnlyMemory<byte>, IConnection[, CancellationToken]). " +
                    "Second parameter must implement IConnection.");
            }

            if (parms.Length == 2)
            {
                return SignatureKind.MemoryNoToken;
            }

            if (parms.Length == 3 && parms[2].ParameterType == typeof(CancellationToken))
            {
                return SignatureKind.MemoryWithToken;
            }

            throw new InternalErrorException(
                $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
                "raw memory signature only supports 2 or 3 parameters " +
                $"(ReadOnlyMemory<byte>, IConnection[, CancellationToken]). Found {parms.Length}.");
        }

        // ---- legacy-style: first param must implement IPacket ----
        if (parms.Length >= 1 && typeof(IPacket).IsAssignableFrom(parms[0].ParameterType))
        {
            if (parms.Length < 2 || !typeof(IConnection).IsAssignableFrom(parms[1].ParameterType))
            {
                throw new InternalErrorException(
                    $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
                    "legacy signature requires (TPacket, IConnection[, CancellationToken]). " +
                    "Second parameter must implement IConnection.");
            }

            // Determine whether the packet parameter is the exact dispatcher TPacket or a
            // concrete subtype. Concrete subtypes get their own SignatureKind variants so
            // the expression-tree builder can emit the correct cast and the runtime
            // ExpectedPacketType check knows which concrete type to verify.
            bool isConcrete = parms[0].ParameterType != typeof(TPacket)
                && typeof(IPacket).IsAssignableFrom(parms[0].ParameterType);

            if (parms.Length == 2)
            {
                return isConcrete
                    ? SignatureKind.LegacyConcreteNoToken
                    : SignatureKind.LegacyNoToken;
            }

            if (parms.Length == 3 && parms[2].ParameterType == typeof(CancellationToken))
            {
                return isConcrete
                    ? SignatureKind.LegacyConcreteWithToken
                    : SignatureKind.LegacyWithToken;
            }

            throw new InternalErrorException(
                $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
                "legacy signature only supports 2 or 3 parameters " +
                $"(TPacket, IConnection[, CancellationToken]). Found {parms.Length}.");
        }

        throw new InternalErrorException(
            $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
            "unrecognised signature. " +
            "Supported forms: " +
            "(TPacket, IConnection), " +
            "(TPacket, IConnection, CancellationToken), " +
            "(TConcretePacket, IConnection), " +
            "(TConcretePacket, IConnection, CancellationToken), " +
            "(PacketContext<T>), " +
            "(PacketContext<T>, CancellationToken), " +
            "(ReadOnlyMemory<byte>, IConnection), " +
            "(ReadOnlyMemory<byte>, IConnection, CancellationToken).");
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> is a closed generic
    /// constructed from <see cref="PacketContext{TPacket}"/>, regardless of which
    /// concrete type argument was supplied.
    /// </summary>
    /// <param name="type"></param>
    /// <remarks>
    /// Using <c>GetGenericTypeDefinition()</c> instead of exact-type equality (==) is
    /// required because the dispatcher may be registered with <c>TPacket = IPacket</c>
    /// while individual handler methods declare <c>PacketContext&lt;LoginPacket&gt;</c>.
    /// The two closed generics are different <see cref="Type"/> objects, so
    /// <c>== typeof(PacketContext&lt;TPacket&gt;)</c> would incorrectly return
    /// <see langword="false"/> and cause the compiler to fall through to the legacy-style
    /// check, ultimately throwing "unrecognised signature".
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IS_PACKET_CONTEXT_TYPE(Type type)
        => type.IsGenericType
        && (type.GetGenericTypeDefinition() == typeof(PacketContext<>) || type.GetGenericTypeDefinition() == typeof(IPacketContext<>));

    /// <summary>
    /// Builds the argument expression array for the compiled method-call expression.
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="parms"></param>
    /// <param name="context"></param>
    /// <param name="packetExpr"></param>
    /// <param name="connectionExpr"></param>
    /// <param name="ctExpr"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Expression[] BUILD_ARG_EXPRESSIONS(
        SignatureKind kind, ParameterInfo[] parms, ParameterExpression context,
        MemberExpression packetExpr, MemberExpression connectionExpr, MemberExpression ctExpr)
    {
        switch (kind)
        {
            case SignatureKind.ContextOnly:
                {
                    // The handler's first param is PacketContext<T> — T may be a concrete
                    // type (e.g. LoginPacket) while the expression-tree parameter is typed
                    // as PacketContext<TPacket> (e.g. PacketContext<IPacket>).
                    // Insert a Convert node when the types differ so the compiled delegate
                    // does not throw InvalidCastException at runtime.
                    Type paramCtxType = parms[0].ParameterType;
                    Expression ctxArg =
                        paramCtxType == context.Type
                        ? context
                        : System.Linq.Expressions.Expression.Convert(context, paramCtxType);

                    return [ctxArg];
                }

            case SignatureKind.ContextWithToken:
                {
                    Type paramCtxType = parms[0].ParameterType;
                    Expression ctxArg =
                        paramCtxType == context.Type
                        ? context
                        : System.Linq.Expressions.Expression.Convert(context, paramCtxType);

                    return [ctxArg, ctExpr];
                }

            case SignatureKind.LegacyNoToken:
                {
                    Type packetType = parms[0].ParameterType;
                    Type connType = parms[1].ParameterType;

                    Expression pktArg = packetType.IsAssignableFrom(typeof(TPacket))
                        ? packetExpr
                        : System.Linq.Expressions.Expression.Convert(packetExpr, packetType);

                    Expression connArg = connType == typeof(IConnection)
                        ? connectionExpr
                        : System.Linq.Expressions.Expression.Convert(connectionExpr, connType);

                    return [pktArg, connArg];
                }

            case SignatureKind.LegacyWithToken:
                {
                    Type packetType = parms[0].ParameterType;
                    Type connType = parms[1].ParameterType;

                    Expression pktArg = packetType.IsAssignableFrom(typeof(TPacket))
                        ? packetExpr
                        : System.Linq.Expressions.Expression.Convert(packetExpr, packetType);

                    Expression connArg = connType == typeof(IConnection)
                        ? connectionExpr
                        : System.Linq.Expressions.Expression.Convert(connectionExpr, connType);

                    return [pktArg, connArg, ctExpr];
                }

            case SignatureKind.LegacyConcreteNoToken:
                {
                    // The handler declares a concrete packet subtype (e.g. LoginPacket) that
                    // differs from TPacket. The runtime ExpectedPacketType check in
                    // ExecuteHandlerAsync already guards against mismatched packets, so here
                    // we only need to emit the cast from TPacket to the concrete type.
                    Type packetType = parms[0].ParameterType;
                    Type connType = parms[1].ParameterType;

                    Expression pktArg = Expression.Convert(packetExpr, packetType);

                    Expression connArg = connType == typeof(IConnection)
                        ? connectionExpr
                        : System.Linq.Expressions.Expression.Convert(connectionExpr, connType);

                    return [pktArg, connArg];
                }

            case SignatureKind.LegacyConcreteWithToken:
                {
                    // Same as LegacyConcreteNoToken but includes CancellationToken.
                    Type packetType = parms[0].ParameterType;
                    Type connType = parms[1].ParameterType;

                    Expression pktArg = Expression.Convert(packetExpr, packetType);

                    Expression connArg = connType == typeof(IConnection)
                        ? connectionExpr
                        : System.Linq.Expressions.Expression.Convert(connectionExpr, connType);

                    return [pktArg, connArg, ctExpr];
                }

            case SignatureKind.MemoryNoToken:
                {
                    Type connType = parms[1].ParameterType;
                    Expression rawMemArg = Expression.Property(Expression.Convert(packetExpr, typeof(MemoryPacket)), nameof(MemoryPacket.Memory));

                    Expression connArg = connType == typeof(IConnection)
                        ? connectionExpr
                        : System.Linq.Expressions.Expression.Convert(connectionExpr, connType);

                    return [rawMemArg, connArg];
                }

            case SignatureKind.MemoryWithToken:
                {
                    Type connType = parms[1].ParameterType;
                    Expression rawMemArg = Expression.Property(Expression.Convert(packetExpr, typeof(MemoryPacket)), nameof(MemoryPacket.Memory));

                    Expression connArg = connType == typeof(IConnection)
                        ? connectionExpr
                        : System.Linq.Expressions.Expression.Convert(connectionExpr, connType);

                    return [rawMemArg, connArg, ctExpr];
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    [Pure]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Func<object, PacketContext<TPacket>, ValueTask<object>> BUILD_CONTEXT_BRIDGE_INVOKER(MethodInfo method, ParameterInfo[] parms, SignatureKind kind)
    {
        try
        {
            // This path is only used when a dispatcher registered with a broad packet type
            // (for example IPacket) needs to invoke a handler that declared a concrete
            // PacketContext<TConcrete>/IPacketContext<TConcrete>.
            Type bridgePacketType = parms[0].ParameterType.GetGenericArguments()[0];
            bool withToken = kind == SignatureKind.ContextWithToken;
            Func<object?, ValueTask<object>> normalizer = CREATE_RESULT_NORMALIZER(method.ReturnType);
            MethodInfo bridgeMethod = GET_REQUIRED_METHOD(
                typeof(PacketHandlerCompiler<TController, TPacket>),
                nameof(INVOKE_CONTEXT_BRIDGE_ASYNC),
                BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(bridgePacketType);

            // Use CreateDelegate to avoid per-call params object[] allocation.
            Func<MethodInfo, object, PacketContext<TPacket>, bool, Func<object?, ValueTask<object>>, ValueTask<object>> bridgeInvoker = bridgeMethod.CreateDelegate<
                Func<MethodInfo, object, PacketContext<TPacket>, bool,
                     Func<object?, ValueTask<object>>, ValueTask<object>>>();

            return (instance, context) =>
                bridgeInvoker(method, instance, context, withToken, normalizer);
        }
        catch (TypeInitializationException tie) when (tie.InnerException is InvalidOperationException ioe && ioe.Message.Contains("PacketRegistry is already built", StringComparison.Ordinal))
        {
            // Log helpful message
            throw new InvalidOperationException(
                "PacketRegistry was built too early. Make sure all packet assemblies are loaded " +
                "and handlers are registered BEFORE calling PacketRegistry.Build(). " +
                "See NetworkApplicationBuilder.", tie);
        }
    }

    /// <summary>
    /// Builds a reflection-based invoker for context-style handlers whose declared
    /// <c>PacketContext&lt;T&gt;</c> type differs from <c>PacketContext&lt;TPacket&gt;</c>.
    /// </summary>
    /// <param name="method"></param>
    /// <param name="parms"></param>
    /// <param name="kind"></param>
    /// <remarks>
    /// Generic classes are invariant in C# — <c>PacketContext&lt;IPacket&gt;</c> and
    /// <c>PacketContext&lt;Handshake&gt;</c> share no subtype relationship even when
    /// <c>Handshake : IPacket</c>, so <c>Expression.Convert</c> between them throws
    /// <see cref="InternalErrorException"/> at compile time.
    /// <para>
    /// <c>MethodInfo.Invoke</c> sidesteps this by boxing every argument to
    /// <see cref="object"/> before passing it to the CLR, which then applies
    /// its own runtime assignability check. The concrete <c>PacketContext&lt;Handshake&gt;</c>
    /// object satisfies that check because it IS a <c>PacketContext&lt;Handshake&gt;</c>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    [Pure]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Func<object, PacketContext<TPacket>, object> BUILD_AOT_INVOKER(MethodInfo method, ParameterInfo[] parms, SignatureKind kind)
    {
        // Use MethodInfo.CreateDelegate to create strongly-typed delegates once,
        // avoiding per-call params object[] allocation and argument boxing that
        // MethodInfo.Invoke requires. The delegate is cached in the closure and
        // invoked directly on subsequent calls.
        bool isVoid = method.ReturnType == typeof(void);

        return kind switch
        {
            SignatureKind.ContextOnly => BUILD_CONTEXT_ONLY_INVOKER(method, isVoid),

            SignatureKind.ContextWithToken => BUILD_CONTEXT_WITH_TOKEN_INVOKER(method, isVoid),

            SignatureKind.LegacyNoToken => BUILD_LEGACY_INVOKER(method, parms, withToken: false),

            SignatureKind.LegacyWithToken => BUILD_LEGACY_INVOKER(method, parms, withToken: true),

            // Concrete packet subtype — cast context.Packet to the declared concrete type.
            // The ExpectedPacketType guard in ExecuteHandlerAsync ensures the runtime packet
            // is actually that concrete type before this invoker is reached.
            SignatureKind.LegacyConcreteNoToken => BUILD_LEGACY_INVOKER(method, parms, withToken: false),

            SignatureKind.LegacyConcreteWithToken => BUILD_LEGACY_INVOKER(method, parms, withToken: true),

            SignatureKind.MemoryNoToken => BUILD_RAW_MEMORY_INVOKER(method, isVoid, withToken: false),

            SignatureKind.MemoryWithToken => BUILD_RAW_MEMORY_INVOKER(method, isVoid, withToken: true),

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Func<object, PacketContext<TPacket>, object> BUILD_RAW_MEMORY_INVOKER(MethodInfo method, bool isVoid, bool withToken)
    {
        if (method.IsStatic)
        {
            if (!withToken)
            {
                if (isVoid)
                {
                    Action<ReadOnlyMemory<byte>, IConnection> invoker = method.CreateDelegate<Action<ReadOnlyMemory<byte>, IConnection>>();
                    return (_, context) => { invoker(((MemoryPacket)(object)context.Packet!).Memory, context.Connection); return null!; };
                }
                else
                {
                    Func<ReadOnlyMemory<byte>, IConnection, object> invoker = method.CreateDelegate<Func<ReadOnlyMemory<byte>, IConnection, object>>();
                    return (_, context) => invoker(((MemoryPacket)(object)context.Packet!).Memory, context.Connection);
                }
            }
            else
            {
                if (isVoid)
                {
                    Action<ReadOnlyMemory<byte>, IConnection, CancellationToken> invoker = method.CreateDelegate<Action<ReadOnlyMemory<byte>, IConnection, CancellationToken>>();
                    return (_, context) => { invoker(((MemoryPacket)(object)context.Packet!).Memory, context.Connection, context.CancellationToken); return null!; };
                }
                else
                {
                    Func<ReadOnlyMemory<byte>, IConnection, CancellationToken, object> invoker = method.CreateDelegate<Func<ReadOnlyMemory<byte>, IConnection, CancellationToken, object>>();
                    return (_, context) => invoker(((MemoryPacket)(object)context.Packet!).Memory, context.Connection, context.CancellationToken);
                }
            }
        }
        else
        {
            if (!withToken)
            {
                if (isVoid)
                {
                    Action<TController, ReadOnlyMemory<byte>, IConnection> invoker = method.CreateDelegate<Action<TController, ReadOnlyMemory<byte>, IConnection>>();
                    return (instance, context) => { invoker((TController)instance, ((MemoryPacket)(object)context.Packet!).Memory, context.Connection); return null!; };
                }
                else
                {
                    Func<TController, ReadOnlyMemory<byte>, IConnection, object> invoker = method.CreateDelegate<Func<TController, ReadOnlyMemory<byte>, IConnection, object>>();
                    return (instance, context) => invoker((TController)instance, ((MemoryPacket)(object)context.Packet!).Memory, context.Connection);
                }
            }
            else
            {
                if (isVoid)
                {
                    Action<TController, ReadOnlyMemory<byte>, IConnection, CancellationToken> invoker = method.CreateDelegate<Action<TController, ReadOnlyMemory<byte>, IConnection, CancellationToken>>();
                    return (instance, context) => { invoker((TController)instance, ((MemoryPacket)(object)context.Packet!).Memory, context.Connection, context.CancellationToken); return null!; };
                }
                else
                {
                    Func<TController, ReadOnlyMemory<byte>, IConnection, CancellationToken, object> invoker = method.CreateDelegate<Func<TController, ReadOnlyMemory<byte>, IConnection, CancellationToken, object>>();
                    return (instance, context) => invoker((TController)instance, ((MemoryPacket)(object)context.Packet!).Memory, context.Connection, context.CancellationToken);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Func<object, PacketContext<TPacket>, object> BUILD_CONTEXT_ONLY_INVOKER(MethodInfo method, bool isVoid)
    {
        if (method.IsStatic)
        {
            if (isVoid)
            {
                Action<PacketContext<TPacket>> invoker = method.CreateDelegate<Action<PacketContext<TPacket>>>();
                return (_, context) => { invoker(context); return null!; };
            }
            else
            {
                Func<PacketContext<TPacket>, object> invoker = method.CreateDelegate<Func<PacketContext<TPacket>, object>>();
                return (_, context) => invoker(context);
            }
        }
        else
        {
            if (isVoid)
            {
                Action<TController, PacketContext<TPacket>> invoker = method.CreateDelegate<Action<TController, PacketContext<TPacket>>>();
                return (instance, context) => { invoker((TController)instance, context); return null!; };
            }
            else
            {
                Func<TController, PacketContext<TPacket>, object> invoker = method.CreateDelegate<Func<TController, PacketContext<TPacket>, object>>();
                return (instance, context) => invoker((TController)instance, context);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Func<object, PacketContext<TPacket>, object> BUILD_CONTEXT_WITH_TOKEN_INVOKER(MethodInfo method, bool isVoid)
    {
        if (method.IsStatic)
        {
            if (isVoid)
            {
                Action<PacketContext<TPacket>, CancellationToken> invoker = method.CreateDelegate<Action<PacketContext<TPacket>, CancellationToken>>();
                return (_, context) => { invoker(context, context.CancellationToken); return null!; };
            }
            else
            {
                Func<PacketContext<TPacket>, CancellationToken, object> invoker = method.CreateDelegate<Func<PacketContext<TPacket>, CancellationToken, object>>();
                return (_, context) => invoker(context, context.CancellationToken);
            }
        }
        else
        {
            if (isVoid)
            {
                Action<TController, PacketContext<TPacket>, CancellationToken> invoker = method.CreateDelegate<Action<TController, PacketContext<TPacket>, CancellationToken>>();
                return (instance, context) => { invoker((TController)instance, context, context.CancellationToken); return null!; };
            }
            else
            {
                Func<TController, PacketContext<TPacket>, CancellationToken, object> invoker = method.CreateDelegate<Func<TController, PacketContext<TPacket>, CancellationToken, object>>();
                return (instance, context) => invoker((TController)instance, context, context.CancellationToken);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Func<object, PacketContext<TPacket>, object> BUILD_LEGACY_INVOKER(MethodInfo method, ParameterInfo[] parms, bool withToken)
    {
        // Cache parameter types outside the lambda to avoid repeated reflection.
        Type packetType = parms[0].ParameterType;

        // When the handler's packet type matches TPacket exactly, we can use
        // CreateDelegate for a zero-allocation direct call. Otherwise, fall back
        // to MethodInfo.Invoke with a reused object[] buffer.
        bool typesMatch = packetType == typeof(TPacket);

        if (typesMatch)
        {
            return BUILD_LEGACY_TYPED_INVOKER(method);
        }

        // Types don't match (e.g. handler expects LoginPacket but TPacket = IPacket).
        // Cache MethodInfo and param types to minimize reflection overhead per call.
        return BUILD_LEGACY_FALLBACK_INVOKER(method, withToken);
    }

    /// <summary>
    /// Fast path: handler's packet type matches TPacket exactly.
    /// Uses CreateDelegate for zero-allocation direct invocation.
    /// </summary>
    private static Func<object, PacketContext<TPacket>, object> BUILD_LEGACY_TYPED_INVOKER(MethodInfo method)
    {
        bool isVoid = method.ReturnType == typeof(void);
        bool isStatic = method.IsStatic;

        // Build delegate type: Func<TController, TPacket, IConnection[, CancellationToken], object>
        // or static: Func<TPacket, IConnection[, CancellationToken], object>
        bool withToken = method.GetParameters().Length == 3;

        if (!withToken)
        {
            if (isStatic)
            {
                if (isVoid)
                {
                    Action<TPacket, IConnection> invoker = method.CreateDelegate<Action<TPacket, IConnection>>();
                    return (_, context) => { invoker(context.Packet, context.Connection); return null!; };
                }
                else
                {
                    Func<TPacket, IConnection, object> invoker = method.CreateDelegate<Func<TPacket, IConnection, object>>();
                    return (_, context) => invoker(context.Packet, context.Connection);
                }
            }
            else
            {
                if (isVoid)
                {
                    Action<TController, TPacket, IConnection> invoker = method.CreateDelegate<Action<TController, TPacket, IConnection>>();
                    return (instance, context) => { invoker((TController)instance, context.Packet, context.Connection); return null!; };
                }
                else
                {
                    Func<TController, TPacket, IConnection, object> invoker = method.CreateDelegate<Func<TController, TPacket, IConnection, object>>();
                    return (instance, context) => invoker((TController)instance, context.Packet, context.Connection);
                }
            }
        }
        else
        {
            if (isStatic)
            {
                if (isVoid)
                {
                    Action<TPacket, IConnection, CancellationToken> invoker = method.CreateDelegate<Action<TPacket, IConnection, CancellationToken>>();
                    return (_, context) => { invoker(context.Packet, context.Connection, context.CancellationToken); return null!; };
                }
                else
                {
                    Func<TPacket, IConnection, CancellationToken, object> invoker = method.CreateDelegate<Func<TPacket, IConnection, CancellationToken, object>>();
                    return (_, context) => invoker(context.Packet, context.Connection, context.CancellationToken);
                }
            }
            else
            {
                if (isVoid)
                {
                    Action<TController, TPacket, IConnection, CancellationToken> invoker = method.CreateDelegate<Action<TController, TPacket, IConnection, CancellationToken>>();
                    return (instance, context) => { invoker((TController)instance, context.Packet, context.Connection, context.CancellationToken); return null!; };
                }
                else
                {
                    Func<TController, TPacket, IConnection, CancellationToken, object> invoker = method.CreateDelegate<Func<TController, TPacket, IConnection, CancellationToken, object>>();
                    return (instance, context) => invoker((TController)instance, context.Packet, context.Connection, context.CancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Fallback path: handler's packet type differs from TPacket.
    /// Uses MethodInfo.Invoke with a reused object[] buffer to minimize allocations.
    /// The array contents are overwritten each call instead of allocating a new array.
    /// </summary>
    private static Func<object, PacketContext<TPacket>, object> BUILD_LEGACY_FALLBACK_INVOKER(MethodInfo method, bool withToken)
    {
        // Allocate the args buffer once and reuse it for every call.
        // This is safe because each invocation is sequential per dispatch worker.
        object[] args = withToken ? new object[4] : new object[3]; // max: [instance, packet, conn, ct]

        if (!withToken)
        {
            return (instance, context) =>
            {
                args[0] = instance;
                args[1] = context.Packet;
                args[2] = context.Connection;
                return method.Invoke(instance, args)!;
            };
        }
        else
        {
            return (instance, context) =>
            {
                args[0] = instance;
                args[1] = context.Packet;
                args[2] = context.Connection;
                args[3] = context.CancellationToken;
                return method.Invoke(instance, args)!;
            };
        }
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Func<object, PacketContext<TPacket>, ValueTask<object>> WRAP_RETURN_TYPE(
        Func<object, PacketContext<TPacket>, object> x00,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type x01)
    {
        // Normalize the handler return type into a single awaitable shape so the
        // dispatcher can treat sync, Task, and ValueTask handlers uniformly.
        if (x01 == typeof(Task))
        {
            return (instance, context) => AWAIT_TASK_VOID_ASYNC(x00(instance, context));
        }

        if (x01.IsGenericType && x01.GetGenericTypeDefinition() == typeof(Task<>))
        {
            Type resultType = x01.GetGenericArguments()[0];
            Func<object, ValueTask<object>> converter = CREATE_TASK_CONVERTER(resultType);
            return (instance, context) => converter(x00(instance, context));
        }

        if (x01 == typeof(ValueTask))
        {
            return (instance, context) => AWAIT_VALUE_TASK_VOID_ASYNC(x00(instance, context));
        }

        if (x01.IsGenericType && x01.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            Type resultType = x01.GetGenericArguments()[0];
            Func<object, ValueTask<object>> converter = CREATE_VALUE_TASK_CONVERTER(resultType);
            return (instance, context) => converter(x00(instance, context));
        }

        return (instance, context) => ValueTask.FromResult(x00(instance, context));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<object?, ValueTask<object>> CREATE_RESULT_NORMALIZER(Type returnType)
    {
        if (returnType == typeof(Task))
        {
            return result => AWAIT_TASK_VOID_ASYNC(result!);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            Type resultType = returnType.GetGenericArguments()[0];
            Func<object, ValueTask<object>> converter = CREATE_TASK_CONVERTER(resultType);
            return result => converter(result!);
        }

        if (returnType == typeof(ValueTask))
        {
            return result => AWAIT_VALUE_TASK_VOID_ASYNC(result!);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            Type resultType = returnType.GetGenericArguments()[0];
            Func<object, ValueTask<object>> converter = CREATE_VALUE_TASK_CONVERTER(resultType);
            return result => converter(result!);
        }

        return result => ValueTask.FromResult(result!);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static async ValueTask<object> INVOKE_CONTEXT_BRIDGE_ASYNC<TConcretePacket>(
        MethodInfo method, object instance, PacketContext<TPacket> context,
        bool withToken, Func<object?, ValueTask<object>> normalizer)
        where TConcretePacket : IPacket
    {
        if (context.Packet is not TConcretePacket concretePacket)
        {
            throw new InternalErrorException(
                $"Handler bridge expected packet type '{typeof(TConcretePacket).Name}' but received '{context.Packet?.GetType().Name ?? "null"}'.");
        }

        ObjectPoolManager pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();
        PacketContext<TConcretePacket> bridgedContext = pool.Get<PacketContext<TConcretePacket>>();

        try
        {
            bridgedContext.Initialize(concretePacket, context.Connection, context.Attributes, context.IsReliable, context.CancellationToken);
            bridgedContext.SkipOutbound = context.SkipOutbound;

            object? result = withToken
                ? method.IsStatic
                    ? method.Invoke(null, [bridgedContext, bridgedContext.CancellationToken])
                    : method.Invoke(instance, [bridgedContext, bridgedContext.CancellationToken])
                : method.IsStatic
                    ? method.Invoke(null, [bridgedContext])
                    : method.Invoke(instance, [bridgedContext]);

            object normalized = await normalizer(result).ConfigureAwait(false);
            context.SkipOutbound = bridgedContext.SkipOutbound;
            return normalized;
        }
        finally
        {
            bridgedContext.Return();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<object, ValueTask<object>> CREATE_TASK_CONVERTER(Type resultType)
    {
        // Reuse the generic async helper instead of building a new wrapper per type.
        MethodInfo method = GET_REQUIRED_METHOD(
            typeof(PacketHandlerCompiler<TController, TPacket>),
            nameof(AWAIT_TASK_RESULT_ASYNC),
            BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(resultType);

        return (Func<object, ValueTask<object>>)Delegate.CreateDelegate(typeof(Func<object, ValueTask<object>>), method);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<object, ValueTask<object>> CREATE_VALUE_TASK_CONVERTER(Type resultType)
    {
        // Same idea for ValueTask<T>: bind the generic helper once, then cache the delegate.
        MethodInfo method = GET_REQUIRED_METHOD(
            typeof(PacketHandlerCompiler<TController, TPacket>),
            nameof(AWAIT_VALUE_TASK_RESULT_ASYNC),
            BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(resultType);

        return (Func<object, ValueTask<object>>)Delegate.CreateDelegate(typeof(Func<object, ValueTask<object>>), method);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static async ValueTask<object> AWAIT_TASK_VOID_ASYNC(object result)
    {
        // Await the task for its side effects, then normalize the result to null.
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
        }

        return null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static async ValueTask<object> AWAIT_TASK_RESULT_ASYNC<TResult>(object result)
    {
        // Return the typed task result as object so the outer pipeline stays generic.
        if (result is Task<TResult> task)
        {
            TResult value = await task.ConfigureAwait(false);
            return value!;
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static async ValueTask<object> AWAIT_VALUE_TASK_VOID_ASYNC(object result)
    {
        // Await the ValueTask for completion and normalize the result to null.
        if (result is ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
        }

        return null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static async ValueTask<object> AWAIT_VALUE_TASK_RESULT_ASYNC<TResult>(object result)
    {
        // Same normalization step for ValueTask<T>.
        if (result is ValueTask<TResult> valueTask)
        {
            TResult value = await valueTask.ConfigureAwait(false);
            return value!;
        }

        return result;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static PacketMetadata GET_PACKET_METADATA(MethodInfo method)
    {
        return s_attributeCache.GetOrAdd(method, static m =>
        {
            PacketMetadataBuilder builder = new()
            {
                // Core attributes – always populated from the method itself.
                Opcode = CustomAttributeExtensions.GetCustomAttribute<PacketOpcodeAttribute>(m),
                Timeout = CustomAttributeExtensions.GetCustomAttribute<PacketTimeoutAttribute>(m),
                Permission = CustomAttributeExtensions.GetCustomAttribute<PacketPermissionAttribute>(m),
                Encryption = CustomAttributeExtensions.GetCustomAttribute<PacketEncryptionAttribute>(m),
                RateLimit = CustomAttributeExtensions.GetCustomAttribute<PacketRateLimitAttribute>(m),
                ConcurrencyLimit = CustomAttributeExtensions.GetCustomAttribute<PacketConcurrencyLimitAttribute>(m),
                Transport = CustomAttributeExtensions.GetCustomAttribute<PacketTransportAttribute>(m)
            };

            // Let external providers extend or override metadata.
            foreach (IPacketMetadataProvider provider in PacketMetadataProviders.Providers)
            {
                provider.Populate(m, builder);
            }

            return builder.Build();
        });
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static string FORMAT_HANDLER_INFO(string x00, ushort x01, MethodInfo? x02 = null, Type? x03 = null)
    {
        string op = $"opcode=0x{x01:X4}";
        string ctrl = $"controller={x00}";
        string m = x02 is null ? "" : $" method={x02.Name}";
        string sig = x02 is null ? "" : $" sig=({string.Join(",", Enumerable
                                                       .Select(x02
                                                       .GetParameters(), p => p.ParameterType.Name))})->{x03?.Name ?? "void"}";

        return $"{op} {ctrl}{m}{sig}";
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PropertyInfo GET_REQUIRED_PROPERTY(Type type, string name)
        => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
        ?? throw new InternalErrorException($"Required property '{type.FullName}.{name}' was not found.");

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MethodInfo GET_REQUIRED_METHOD(Type type, string name, BindingFlags bindingFlags)
        => type.GetMethod(name, bindingFlags)
        ?? throw new InternalErrorException($"Required method '{type.FullName}.{name}' was not found.");

    #endregion Private Methods
}

