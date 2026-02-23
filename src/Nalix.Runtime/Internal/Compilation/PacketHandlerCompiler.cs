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
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Internal.Results;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

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

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(PacketHandlerCompiler<,>)}:{nameof(CompileHandlers)}] scan controller={controllerType.Name}");

        // Reuse cached method metadata when possible; otherwise compile once and
        // freeze the result so dispatch stays allocation-free at runtime.
        FrozenDictionary<ushort, PacketHandlerDescriptor<TPacket>> compiledMethods = CompileControllerHandlers(controllerType);

        // Create one controller instance up front and reuse it for every handler.
        TController controllerInstance = factory();

        // CreateCatalog delegate descriptors
        PacketHandler<TPacket>[] descriptors = new PacketHandler<TPacket>[compiledMethods.Count];
        int index = 0;

        foreach ((ushort opCode, PacketHandlerDescriptor<TPacket> compiledMethod) in compiledMethods)
        {
            PacketMetadata attributes = GetPacketMetadata(compiledMethod.MethodInfo);

            descriptors[index++] = new PacketHandler<TPacket>(
                opCode,
                attributes,
                controllerInstance,
                compiledMethod.MethodInfo,
                compiledMethod.ReturnType,
                compiledMethod.CompiledInvoker,
                expectedPacketType: null,
                returnHandler: ReturnTypeHandlerFactory<TPacket>.ResolveHandler(compiledMethod.ReturnType));
        }

        string firstOps = string.Join(",", Enumerable
                                              .Select(Enumerable
                                              .Take(compiledMethods.Keys, 6), o => $"0x{o:X4}"));

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(PacketHandlerCompiler<,>)}:{nameof(CompileHandlers)}] " +
                                       $"found count={compiledMethods.Count} controller={controllerType.FullName} ops=[{firstOps}{(compiledMethods.Count > 6 ? ",..." : string.Empty)}]");

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
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static FrozenDictionary<ushort, PacketHandlerDescriptor<TPacket>> CompileControllerHandlers([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type x03)
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

        if (methodInfos.Length == 0)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[NW.{nameof(PacketHandlerCompiler<,>)}:Internal] no-method controller={x03.Name}");
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(PacketHandlerCompiler<,>)}:Internal] compile count={methodInfos.Length} controller={x03.Name}");

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
                    string x01 = FormatHandlerInfo(method.DeclaringType?.Name ?? "None", opcodeAttr.OpCode, method, method.ReturnType);

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[NW.{nameof(PacketHandlerCompiler<,>)}:Internal] dup-opcode {x01}");

                    continue;
                }

                try
                {
                    compiled[opcodeAttr.OpCode] = CompileHandlerMethod(method);

                    string x01 = FormatHandlerInfo(method.DeclaringType?.Name ?? "None", opcodeAttr.OpCode, method, method.ReturnType);

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[NW.{nameof(PacketHandlerCompiler<,>)}:Internal] compiled {x01}");
                }
                catch (Exception ex)
                {
                    string x01 = FormatHandlerInfo(method.DeclaringType?.Name ?? "None", opcodeAttr.OpCode, method, method.ReturnType);

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[NW.{nameof(PacketHandlerCompiler<,>)}:Internal] failed-compile {x01} ex={ex.GetType().Name}", ex);
                }
            }

            return FrozenDictionary.ToFrozenDictionary(compiled);
        }, methodInfos);
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static PacketHandlerDescriptor<TPacket> CompileHandlerMethod(MethodInfo x22)
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
        PropertyInfo packetProperty = GetRequiredProperty(contextType, nameof(PacketContext<>.Packet));
        PropertyInfo connectionProperty = GetRequiredProperty(contextType, nameof(PacketContext<>.Connection));
        PropertyInfo cancellationTokenProperty = GetRequiredProperty(contextType, nameof(PacketContext<>.CancellationToken));

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

        SignatureKind kind = ResolveSignatureKind(x22, parms);

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
        bool needsContextBridge =
            (kind is SignatureKind.ContextOnly or SignatureKind.ContextWithToken)
            && parms[0].ParameterType != typeof(PacketContext<TPacket>);

        Func<object, PacketContext<TPacket>, object> x12;

        if (needsContextBridge)
        {
            x12 = BuildContextBridgeInvoker(x22, parms, kind);
        }
        else if (RuntimeFeature.IsDynamicCodeSupported)
        {
            // Normal expression-tree path — types match exactly.
            Expression[] x09 = BuildArgExpressions(kind, parms, x01, x02, x03, x04);

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
            x12 = BuildAotInvoker(x22, parms, kind);
        }

        Func<object, PacketContext<TPacket>,
            ValueTask<object>> x20 = WrapReturnType(x12, x22.ReturnType);

        return new PacketHandlerDescriptor<TPacket>(x22, x22.ReturnType, x20);
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
    private static SignatureKind ResolveSignatureKind(MethodInfo method, ParameterInfo[] parms)
    {
        // ---- new-style: first param is PacketContext<T> for any T : IPacket ----
        // Use generic-definition comparison instead of exact-type equality so that
        // PacketContext<LoginPacket> is recognised when TPacket = IPacket.
        if (parms.Length >= 1 && IsPacketContextType(parms[0].ParameterType))
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
            "(PacketContext<T>, CancellationToken).");
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
    private static bool IsPacketContextType(Type type)
        => type.IsGenericType
        && type.GetGenericTypeDefinition() == typeof(PacketContext<>);

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
    private static Expression[] BuildArgExpressions(
        SignatureKind kind,
        ParameterInfo[] parms,
        ParameterExpression context,
        MemberExpression packetExpr,
        MemberExpression connectionExpr,
        MemberExpression ctExpr)
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

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    [Pure]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    private static Func<object, PacketContext<TPacket>, object> BuildContextBridgeInvoker(MethodInfo method, ParameterInfo[] parms, SignatureKind kind)
    {
        // Capture once at compile time — zero allocation on the hot path.
        bool isStatic = method.IsStatic;
        bool withToken = kind == SignatureKind.ContextWithToken;

        return (instance, context) =>
        {
            // MethodInfo.Invoke accepts the concrete PacketContext<T> as-is via
            // object boxing, so no coercion operator is needed.
            object[] args = withToken
                ? [context, context.CancellationToken]
                : [context];

            return isStatic
                ? method.Invoke(null, args)!
                : method.Invoke(instance, args)!;
        };
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
    private static Func<object, PacketContext<TPacket>, object> BuildAotInvoker(MethodInfo method, ParameterInfo[] parms, SignatureKind kind)
    {
        return kind switch
        {
            // AOT path: reflection.Invoke accepts the context object as-is because
            // MethodInfo.Invoke boxes arguments to System.Object and performs the
            // runtime assignability check itself — no explicit cast needed here.
            // This differs from the expression-tree path where the IL cast must be
            // explicit (see BuildArgExpressions / IsPacketContextType).
            SignatureKind.ContextOnly =>
                (instance, context) => method.IsStatic
                    ? method.Invoke(null, [context])!
                    : method.Invoke(instance, [context])!,

            SignatureKind.ContextWithToken =>
                (instance, context) => method.IsStatic
                    ? method.Invoke(null, [context, context.CancellationToken])!
                    : method.Invoke(instance, [context, context.CancellationToken])!,

            SignatureKind.LegacyNoToken =>
                (instance, context) =>
                {
                    Type p0 = parms[0].ParameterType;
                    Type p1 = parms[1].ParameterType;

                    object pkt = p0.IsInstanceOfType(context.Packet) ? context.Packet : Convert.ChangeType(context.Packet, p0, provider: null)!;
                    object conn = p1.IsInstanceOfType(context.Connection) ? context.Connection : Convert.ChangeType(context.Connection, p1, provider: null)!;

                    return method.IsStatic
                        ? method.Invoke(null, [pkt, conn])!
                        : method.Invoke(instance, [pkt, conn])!;
                }
            ,

            SignatureKind.LegacyWithToken =>
                (instance, context) =>
                {
                    Type p0 = parms[0].ParameterType;
                    Type p1 = parms[1].ParameterType;

                    object pkt = p0.IsInstanceOfType(context.Packet) ? context.Packet : Convert.ChangeType(context.Packet, p0, provider: null)!;
                    object conn = p1.IsInstanceOfType(context.Connection) ? context.Connection : Convert.ChangeType(context.Connection, p1, provider: null)!;

                    return method.IsStatic
                        ? method.Invoke(null, [pkt, conn, context.CancellationToken])!
                        : method.Invoke(instance, [pkt, conn, context.CancellationToken])!;
                }
            ,

            // Concrete packet subtype — cast context.Packet to the declared concrete type.
            // The ExpectedPacketType guard in ExecuteHandlerAsync ensures the runtime packet
            // is actually that concrete type before this invoker is reached.
            SignatureKind.LegacyConcreteNoToken =>
                (instance, context) =>
                {
                    Type p0 = parms[0].ParameterType;
                    Type p1 = parms[1].ParameterType;

                    // Best-effort cast: if the packet is already the right type use it directly,
                    // otherwise let Convert.ChangeType attempt a coercion (rare path).
                    object pkt = p0.IsInstanceOfType(context.Packet) ? context.Packet : Convert.ChangeType(context.Packet, p0, provider: null)!;
                    object conn = p1.IsInstanceOfType(context.Connection) ? context.Connection : Convert.ChangeType(context.Connection, p1, provider: null)!;

                    return method.IsStatic
                        ? method.Invoke(null, [pkt, conn])!
                        : method.Invoke(instance, [pkt, conn])!;
                }
            ,

            SignatureKind.LegacyConcreteWithToken =>
                (instance, context) =>
                {
                    Type p0 = parms[0].ParameterType;
                    Type p1 = parms[1].ParameterType;

                    object pkt = p0.IsInstanceOfType(context.Packet) ? context.Packet : Convert.ChangeType(context.Packet, p0, provider: null)!;
                    object conn = p1.IsInstanceOfType(context.Connection) ? context.Connection : Convert.ChangeType(context.Connection, p1, provider: null)!;

                    return method.IsStatic
                        ? method.Invoke(null, [pkt, conn, context.CancellationToken])!
                        : method.Invoke(instance, [pkt, conn, context.CancellationToken])!;
                }
            ,

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Func<object, PacketContext<TPacket>, ValueTask<object>> WrapReturnType(
        Func<object, PacketContext<TPacket>, object> x00,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type x01)
    {
        // Normalize the handler return type into a single awaitable shape so the
        // dispatcher can treat sync, Task, and ValueTask handlers uniformly.
        if (x01 == typeof(Task))
        {
            return (instance, context) => AwaitTaskVoidAsync(x00(instance, context));
        }

        if (x01.IsGenericType && x01.GetGenericTypeDefinition() == typeof(Task<>))
        {
            Type resultType = x01.GetGenericArguments()[0];
            Func<object, ValueTask<object>> converter = CreateTaskConverter(resultType);
            return (instance, context) => converter(x00(instance, context));
        }

        if (x01 == typeof(ValueTask))
        {
            return (instance, context) => AwaitValueTaskVoidAsync(x00(instance, context));
        }

        if (x01.IsGenericType && x01.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            Type resultType = x01.GetGenericArguments()[0];
            Func<object, ValueTask<object>> converter = CreateValueTaskConverter(resultType);
            return (instance, context) => converter(x00(instance, context));
        }

        return (instance, context) => ValueTask.FromResult(x00(instance, context));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<object, ValueTask<object>> CreateTaskConverter(Type resultType)
    {
        // Reuse the generic async helper instead of building a new wrapper per type.
        MethodInfo method = GetRequiredMethod(
            typeof(PacketHandlerCompiler<TController, TPacket>),
            nameof(AwaitTaskResultAsync),
            BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(resultType);

        return (Func<object, ValueTask<object>>)Delegate.CreateDelegate(typeof(Func<object, ValueTask<object>>), method);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<object, ValueTask<object>> CreateValueTaskConverter(Type resultType)
    {
        // Same idea for ValueTask<T>: bind the generic helper once, then cache the delegate.
        MethodInfo method = GetRequiredMethod(
            typeof(PacketHandlerCompiler<TController, TPacket>),
            nameof(AwaitValueTaskResultAsync),
            BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(resultType);

        return (Func<object, ValueTask<object>>)Delegate.CreateDelegate(typeof(Func<object, ValueTask<object>>), method);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static async ValueTask<object> AwaitTaskVoidAsync(object result)
    {
        // Await the task for its side effects, then normalize the result to null.
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
        }

        return null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static async ValueTask<object> AwaitTaskResultAsync<TResult>(object result)
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
    private static async ValueTask<object> AwaitValueTaskVoidAsync(object result)
    {
        // Await the ValueTask for completion and normalize the result to null.
        if (result is ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
        }

        return null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static async ValueTask<object> AwaitValueTaskResultAsync<TResult>(object result)
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
    private static PacketMetadata GetPacketMetadata(MethodInfo method)
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
                ConcurrencyLimit = CustomAttributeExtensions.GetCustomAttribute<PacketConcurrencyLimitAttribute>(m)
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
    private static string FormatHandlerInfo(string x00, ushort x01, MethodInfo? x02 = null, Type? x03 = null)
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
    private static PropertyInfo GetRequiredProperty(Type type, string name)
        => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
        ?? throw new InternalErrorException($"Required property '{type.FullName}.{name}' was not found.");

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MethodInfo GetRequiredMethod(Type type, string name, BindingFlags bindingFlags)
        => type.GetMethod(name, bindingFlags)
        ?? throw new InternalErrorException($"Required method '{type.FullName}.{name}' was not found.");

    #endregion Private Methods
}
