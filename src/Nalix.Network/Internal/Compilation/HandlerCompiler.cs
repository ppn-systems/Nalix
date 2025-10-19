// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Diagnostics;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.Network.Routing;
using Nalix.Network.Routing.Metadata;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Compilation;

/// <summary>
/// High-performance controller scanner with caching and zero-allocation lookups.
/// Uses compiled expression trees for maximum dispatch performance.
/// </summary>
/// <typeparam name="TController">The controller type to scan.</typeparam>
/// <typeparam name="TPacket">The packet type handled by this controller.</typeparam>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal sealed class HandlerCompiler<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] TController, TPacket>()
    where TController : class where TPacket : IPacket
{
    #region Fields

    /// <summary>
    /// Caches compiled method delegates for each controller type to eliminate reflection.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        Type, System.Collections.Frozen.FrozenDictionary<
            ushort, CompiledHandler<TPacket>>> _compiledMethodCache = new();

    /// <summary>
    /// Caches attribute lookups per method for performance.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.Reflection.MethodInfo, PacketMetadata> _attributeCache = new();

    #endregion Fields

    /// <summary>
    /// Scans the controller and returns an array of packet handler delegates.
    /// </summary>
    /// <param name="factory">A factory method that creates a controller instance.</param>
    /// <returns>An array of compiled packet handler delegates.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static PacketHandler<TPacket>[] CompileHandlers(Func<TController> factory)
    {
        Type controllerType = typeof(TController);

        // Ensure controller has [PacketController] attribute
        PacketControllerAttribute controllerAttr = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketControllerAttribute>(controllerType)
            ?? throw new InvalidOperationException($"Controller '{controllerType.Name}' is missing the [PacketController] attribute.");

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(HandlerCompiler<,>)}:{nameof(CompileHandlers)}] scan controller={controllerType.Name}");

        // Get or compile all handler methods
        System.Collections.Frozen.FrozenDictionary<ushort, CompiledHandler<TPacket>> compiledMethods = CompileControllerHandlers(controllerType);

        // Create the controller instance
        TController controllerInstance = factory();

        // CreateCatalog delegate descriptors
        PacketHandler<TPacket>[] descriptors = new PacketHandler<TPacket>[compiledMethods.Count];
        int index = 0;

        foreach ((ushort opCode, CompiledHandler<TPacket> compiledMethod) in compiledMethods)
        {
            PacketMetadata attributes = GetPacketMetadata(compiledMethod.MethodInfo);

            descriptors[index++] = new PacketHandler<TPacket>(
                opCode,
                attributes,
                controllerInstance,
                compiledMethod.MethodInfo,
                compiledMethod.ReturnType,
                compiledMethod.CompiledInvoker);
        }

        string firstOps = string.Join(",", System.Linq.Enumerable
                                              .Select(System.Linq.Enumerable
                                              .Take(compiledMethods.Keys, 6), o => $"0x{o:X4}"));

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(HandlerCompiler<,>)}:{nameof(CompileHandlers)}] " +
                                       $"found count={compiledMethods.Count} controller={controllerType.FullName} ops=[{firstOps}{(compiledMethods.Count > 6 ? ",..." : string.Empty)}]");

        return descriptors;
    }

    #region Private Methods

    /// <summary>
    /// Describes the recognized parameter signature of a handler method.
    /// </summary>
    private enum SignatureKind
    {
        /// <summary>(TPacket, IConnection)</summary>
        LegacyNoToken = 0,

        /// <summary>(TPacket, IConnection, CancellationToken)</summary>
        LegacyWithToken = 1,

        /// <summary>(PacketContext&lt;TPacket&gt;)</summary>
        ContextOnly = 2,

        /// <summary>(PacketContext&lt;TPacket&gt;, CancellationToken)</summary>
        ContextWithToken = 3,
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Collections.Frozen.FrozenDictionary<ushort, CompiledHandler<TPacket>> CompileControllerHandlers(
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] Type x03)
    {
        // Get methods with [PacketOpcode] attribute
        System.Reflection.MethodInfo[] methodInfos = System.Linq.Enumerable.ToArray(
            System.Linq.Enumerable.Where(
                x03.GetMethods(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static
                ),
                m => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketOpcodeAttribute>(m) is not null));

        if (methodInfos.Length == 0)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[NW.{nameof(HandlerCompiler<,>)}:Internal] no-method controller={x03.Name}");
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(HandlerCompiler<,>)}:Internal] compile count={methodInfos.Length} controller={x03.Name}");

        return _compiledMethodCache.GetOrAdd(x03, static (_, methods) =>
        {
            System.Collections.Generic.Dictionary<ushort, CompiledHandler<TPacket>> compiled = new(methods.Length);

            foreach (System.Reflection.MethodInfo method in methods)
            {
                PacketOpcodeAttribute opcodeAttr = System.Reflection.CustomAttributeExtensions
                    .GetCustomAttribute<PacketOpcodeAttribute>(method);

                if (compiled.ContainsKey(opcodeAttr.OpCode))
                {
                    string x01 = FormatHandlerInfo(method.DeclaringType?.Name ?? "NONE", opcodeAttr.OpCode, method, method.ReturnType);

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[NW.{nameof(HandlerCompiler<,>)}:Internal] dup-opcode {x01}");

                    continue;
                }

                try
                {
                    compiled[opcodeAttr.OpCode] = CompileHandlerMethod(method);

                    string x01 = FormatHandlerInfo(method.DeclaringType?.Name ?? "NONE", opcodeAttr.OpCode, method, method.ReturnType);

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[NW.{nameof(HandlerCompiler<,>)}:Internal] compiled {x01}");
                }
                catch (Exception ex)
                {
                    string x01 = FormatHandlerInfo(method.DeclaringType?.Name ?? "NONE", opcodeAttr.OpCode, method, method.ReturnType);

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[NW.{nameof(HandlerCompiler<,>)}:Internal] failed-compile {x01} ex={ex.GetType().Name}", ex);
                }
            }

            return System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(compiled);
        }, methodInfos);
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static CompiledHandler<TPacket> CompileHandlerMethod(System.Reflection.MethodInfo x22)
    {
        // -------------------------------------------------------------------
        // Shared expression nodes — always built regardless of signature kind
        // x00 = boxed controller instance
        // x01 = PacketContext<TPacket>  (the single source-of-truth arg the
        //        compiled invoker always receives from ExecuteHandlerAsync)
        // x02..x04 = property reads off x01
        // -------------------------------------------------------------------
        System.Linq.Expressions.ParameterExpression x00 =
            System.Linq.Expressions.Expression.Parameter(typeof(object), "instance");

        System.Linq.Expressions.ParameterExpression x01 =
            System.Linq.Expressions.Expression.Parameter(typeof(PacketContext<TPacket>), "context");

        Type contextType = typeof(PacketContext<TPacket>);

        System.Linq.Expressions.MemberExpression x02 =
            System.Linq.Expressions.Expression.Property(x01, contextType.GetProperty(nameof(PacketContext<>.Packet)));

        System.Linq.Expressions.MemberExpression x03 =
            System.Linq.Expressions.Expression.Property(x01, contextType.GetProperty(nameof(PacketContext<>.Connection)));

        System.Linq.Expressions.MemberExpression x04 =
            System.Linq.Expressions.Expression.Property(x01, contextType.GetProperty(nameof(PacketContext<>.CancellationToken)));

        // -------------------------------------------------------------------
        // Detect which of the 4 supported signatures this method uses.
        // Supported forms:
        //   Legacy  (a) (TPacket, IConnection)
        //   Legacy  (b) (TPacket, IConnection, CancellationToken)
        //   New     (c) (PacketContext<TPacket>)
        //   New     (d) (PacketContext<TPacket>, CancellationToken)
        // -------------------------------------------------------------------
        System.Reflection.ParameterInfo[] parms = x22.GetParameters();

        SignatureKind kind = ResolveSignatureKind(x22, parms);

        // -------------------------------------------------------------------
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
        // -------------------------------------------------------------------
        bool needsContextBridge =
            (kind is SignatureKind.ContextOnly or SignatureKind.ContextWithToken)
            && parms[0].ParameterType != typeof(PacketContext<TPacket>);

        Func<object, PacketContext<TPacket>, object> x12;

        if (needsContextBridge)
        {
            x12 = BuildContextBridgeInvoker(x22, parms, kind);
        }
        else if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
        {
            // ---------------------------------------------------------------
            // Normal expression-tree path — types match exactly.
            // ---------------------------------------------------------------
            System.Linq.Expressions.Expression[] x09 = BuildArgExpressions(kind, parms, x01, x02, x03, x04);

            System.Linq.Expressions.Expression x10 = x22.IsStatic
                ? System.Linq.Expressions.Expression.Call(x22, x09)
                : System.Linq.Expressions.Expression.Call(
                    System.Linq.Expressions.Expression.Convert(x00, x22.DeclaringType), x22, x09);

            System.Linq.Expressions.Expression x11 = x22.ReturnType == typeof(void)
                ? System.Linq.Expressions.Expression.Block(x10, System.Linq.Expressions.Expression.Constant(null, typeof(object)))
                : System.Linq.Expressions.Expression.Convert(x10, typeof(object));

            x12 = System.Linq.Expressions.Expression
                    .Lambda<Func<object, PacketContext<TPacket>, object>>(x11, x00, x01)
                    .Compile();
        }
        else
        {
            // AOT fallback — build invoke args at call-time from context fields.
            x12 = BuildAotInvoker(x22, parms, kind);
        }

        Func<object, PacketContext<TPacket>,
            System.Threading.Tasks.ValueTask<object>> x20 = WrapReturnType(x12, x22.ReturnType);

        return new CompiledHandler<TPacket>(x22, x22.ReturnType, x20);
    }

    /// <summary>
    /// Determines the <see cref="SignatureKind"/> of a handler method.
    /// Throws <see cref="InvalidOperationException"/> for unrecognised signatures.
    /// </summary>
    /// <param name="method"></param>
    /// <param name="parms"></param>
    /// <exception cref="InvalidOperationException"></exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static SignatureKind ResolveSignatureKind(
        System.Reflection.MethodInfo method,
        System.Reflection.ParameterInfo[] parms)
    {
        // ---- new-style: first param is PacketContext<T> for any T : IPacket ----
        // Use generic-definition comparison instead of exact-type equality so that
        // PacketContext<LoginPacket> is recognised when TPacket = IPacket.
        // Exact equality (== typeof(PacketContext<TPacket>)) would reject any handler
        // whose first parameter is a concrete PacketContext<ConcreteType> whenever the
        // dispatcher was registered with the base IPacket interface as TPacket.
        if (parms.Length >= 1 && IsPacketContextType(parms[0].ParameterType))
        {
            // Validate that the generic type argument matches TPacket exactly.
            // PacketContext<T> is invariant — PacketContext<Handshake> and
            // PacketContext<IPacket> are unrelated types with no legal conversion,
            // even when Handshake : IPacket. Accepting a mismatched T here would
            // compile the expression tree correctly but crash at runtime with
            // "No coercion operator defined". Catch it early with a clear message.
            Type declaredT = parms[0].ParameterType.GetGenericArguments()[0];
            if (declaredT != typeof(TPacket))
            {
                throw new InvalidOperationException(
                    $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
                    $"parameter type PacketContext<{declaredT.Name}> does not match " +
                    $"the dispatcher's TPacket={typeof(TPacket).Name}. " +
                    $"Declare the parameter as PacketContext<{typeof(TPacket).Name}> " +
                    $"and cast context.Packet to {declaredT.Name} inside the method body: " +
                    $"var pkt = ({declaredT.Name})context.Packet;");
            }
            else if (parms.Length == 1)
            {
                return SignatureKind.ContextOnly;
            }
            else
            {
                return parms.Length == 2 && parms[1].ParameterType == typeof(System.Threading.CancellationToken)
                    ? SignatureKind.ContextWithToken
                    : throw new InvalidOperationException(
                            $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
                            "when the first parameter is PacketContext<TPacket>, " +
                            "the only valid second parameter is CancellationToken. " +
                            $"Found {parms.Length} parameter(s).");
            }
        }

        // ---- legacy-style: first param must implement IPacket ----
        if (parms.Length >= 1 && typeof(IPacket).IsAssignableFrom(parms[0].ParameterType))
        {
            if (parms.Length < 2 || !typeof(IConnection).IsAssignableFrom(parms[1].ParameterType))
            {
                // ---- legacy-style: first param must implement IPacket ----
                throw new InvalidOperationException(
                    $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
                    "legacy signature requires (TPacket, IConnection[, CancellationToken]). " +
                    "Second parameter must implement IConnection.");
            }
            else if (parms.Length == 2)
            {
                // ---- legacy-style: first param must implement IPacket ----
                return SignatureKind.LegacyNoToken;
            }
            else if (parms.Length == 3 && parms[2].ParameterType == typeof(System.Threading.CancellationToken))
            {
                // ---- legacy-style: first param must implement IPacket ----
                return SignatureKind.LegacyWithToken;
            }
            else
            {
                // ---- legacy-style: first param must implement IPacket ----
                throw new InvalidOperationException(
                    $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
                    "legacy signature only supports 2 or 3 parameters " +
                    $"(TPacket, IConnection[, CancellationToken]). Found {parms.Length}.");
            }
        }
        else
        {
            // ---- legacy-style: first param must implement IPacket ----
            throw new InvalidOperationException(
                $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
                "unrecognised signature. " +
                "Supported forms: " +
                "(TPacket, IConnection), " +
                "(TPacket, IConnection, CancellationToken), " +
                "(PacketContext<T>), " +
                "(PacketContext<T>, CancellationToken).");
        }
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
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Linq.Expressions.Expression[] BuildArgExpressions(
        SignatureKind kind,
        System.Reflection.ParameterInfo[] parms,
        System.Linq.Expressions.ParameterExpression context,
        System.Linq.Expressions.MemberExpression packetExpr,
        System.Linq.Expressions.MemberExpression connectionExpr,
        System.Linq.Expressions.MemberExpression ctExpr)
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
                    System.Linq.Expressions.Expression ctxArg =
                        paramCtxType == context.Type
                        ? context
                        : System.Linq.Expressions.Expression.Convert(context, paramCtxType);

                    return [ctxArg];
                }

            case SignatureKind.ContextWithToken:
                {
                    Type paramCtxType = parms[0].ParameterType;
                    System.Linq.Expressions.Expression ctxArg =
                        paramCtxType == context.Type
                        ? context
                        : System.Linq.Expressions.Expression.Convert(context, paramCtxType);

                    return [ctxArg, ctExpr];
                }

            case SignatureKind.LegacyNoToken:
                {
                    Type packetType = parms[0].ParameterType;
                    Type connType = parms[1].ParameterType;

                    System.Linq.Expressions.Expression pktArg = packetType.IsAssignableFrom(typeof(TPacket))
                        ? packetExpr
                        : System.Linq.Expressions.Expression.Convert(packetExpr, packetType);

                    System.Linq.Expressions.Expression connArg = connType == typeof(IConnection)
                        ? connectionExpr
                        : System.Linq.Expressions.Expression.Convert(connectionExpr, connType);

                    return [pktArg, connArg];
                }

            case SignatureKind.LegacyWithToken:
                {
                    Type packetType = parms[0].ParameterType;
                    Type connType = parms[1].ParameterType;

                    System.Linq.Expressions.Expression pktArg = packetType.IsAssignableFrom(typeof(TPacket))
                        ? packetExpr
                        : System.Linq.Expressions.Expression.Convert(packetExpr, packetType);

                    System.Linq.Expressions.Expression connArg = connType == typeof(IConnection)
                        ? connectionExpr
                        : System.Linq.Expressions.Expression.Convert(connectionExpr, connType);

                    return [pktArg, connArg, ctExpr];
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Func<object, PacketContext<TPacket>, object> BuildContextBridgeInvoker(
        System.Reflection.MethodInfo method,
        System.Reflection.ParameterInfo[] parms,
        SignatureKind kind)
    {
        // Capture once at compile time — zero allocation on the hot path.
        bool isStatic = method.IsStatic;
        bool withToken = kind == SignatureKind.ContextWithToken;

        return (instance, context) =>
        {
            // MethodInfo.Invoke accepts the concrete PacketContext<T> as-is via
            // object boxing — no coercion operator required.
            object[] args = withToken
                ? [context, context.CancellationToken]
                : [context];

            return isStatic
                ? method.Invoke(null, args)
                : method.Invoke(instance, args);
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
    /// <see cref="InvalidOperationException"/> at compile time.
    /// <para>
    /// <c>MethodInfo.Invoke</c> sidesteps this by boxing every argument to
    /// <see cref="object"/> before passing it to the CLR, which then applies
    /// its own runtime assignability check. The concrete <c>PacketContext&lt;Handshake&gt;</c>
    /// object satisfies that check because it IS a <c>PacketContext&lt;Handshake&gt;</c>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Func<object, PacketContext<TPacket>, object> BuildAotInvoker(
    System.Reflection.MethodInfo method,
    System.Reflection.ParameterInfo[] parms,
    SignatureKind kind)
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
                    ? method.Invoke(null, [context])
                    : method.Invoke(instance, [context]),

            SignatureKind.ContextWithToken =>
                (instance, context) => method.IsStatic
                    ? method.Invoke(null, [context, context.CancellationToken])
                    : method.Invoke(instance, [context, context.CancellationToken]),

            SignatureKind.LegacyNoToken =>
                (instance, context) =>
                {
                    Type p0 = parms[0].ParameterType;
                    Type p1 = parms[1].ParameterType;

                    object pkt = p0.IsInstanceOfType(context.Packet) ? context.Packet : Convert.ChangeType(context.Packet, p0, provider: null);
                    object conn = p1.IsInstanceOfType(context.Connection) ? context.Connection : Convert.ChangeType(context.Connection, p1, provider: null);

                    return method.IsStatic
                        ? method.Invoke(null, [pkt, conn])
                        : method.Invoke(instance, [pkt, conn]);
                }
            ,

            SignatureKind.LegacyWithToken =>
                (instance, context) =>
                {
                    Type p0 = parms[0].ParameterType;
                    Type p1 = parms[1].ParameterType;

                    object pkt = p0.IsInstanceOfType(context.Packet) ? context.Packet : Convert.ChangeType(context.Packet, p0, provider: null);
                    object conn = p1.IsInstanceOfType(context.Connection) ? context.Connection : Convert.ChangeType(context.Connection, p1, provider: null);

                    return method.IsStatic
                        ? method.Invoke(null, [pkt, conn, context.CancellationToken])
                        : method.Invoke(instance, [pkt, conn, context.CancellationToken]);
                }
            ,

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static Func<object, PacketContext<TPacket>, System.Threading.Tasks.ValueTask<object>> WrapReturnType(
        Func<object, PacketContext<TPacket>, object> x00,
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] Type x01)
    {
        if (x01 == typeof(System.Threading.Tasks.Task))
        {
            return async (instance, context) =>
            {
                if (x00(instance, context) is System.Threading.Tasks.Task t)
                {
                    await t.ConfigureAwait(false);
                }
                return null;
            };
        }

        if (x01.IsGenericType && x01.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>))
        {
            // Cache Result getter at compile-time for this x01
            System.Reflection.PropertyInfo x02 = x01.GetProperty("Result");
            return async (instance, context) =>
            {
                object r = x00(instance, context);
                if (r is System.Threading.Tasks.Task t)
                {
                    await t.ConfigureAwait(false);
                    return x02.GetValue(t);
                }
                return r;
            };
        }

        if (x01 == typeof(System.Threading.Tasks.ValueTask))
        {
            // Call .GetAwaiter().GetResult() without allocations
            System.Reflection.MethodInfo getAwaiter = typeof(System.Threading.Tasks.ValueTask)
                .GetMethod("GetAwaiter", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            System.Reflection.PropertyInfo x02 = getAwaiter.ReturnType.GetProperty("IsCompleted");
            System.Reflection.MethodInfo x03 = getAwaiter.ReturnType.GetMethod("GetResult");

            return async (instance, context) =>
            {
                object r = x00(instance, context);
                if (r is System.Threading.Tasks.ValueTask vt)
                {
                    // prefer await: lets the compiler pick optimal path
                    await vt.ConfigureAwait(false);
                }
                return null;
            };
        }

        if (x01.IsGenericType && x01.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.ValueTask<>))
        {
            // Build a converter: ValueTask<T> -> Task<T> once, then await Task<T> (no dynamic)
            System.Reflection.PropertyInfo x06 = x01.GetProperty("Result"); // exists but only valid if completed
            System.Reflection.MethodInfo x07 = x01.GetMethod("AsTask", Type.EmptyTypes); // ValueTask<T>.AsTask()

            return async (instance, context) =>
            {
                object r = x00(instance, context);
                if (r is null)
                {
                    return null;
                }

                // x10 AsTask() via reflection once per wrapper
                object x03 = x07.Invoke(r, null); // Task<T>
                System.Threading.Tasks.Task x04 = (System.Threading.Tasks.Task)x03;
                await x04.ConfigureAwait(false);

                // read Task<T>.Result once completed
                System.Reflection.PropertyInfo x05 = x03.GetType().GetProperty("Result");
                return x05.GetValue(x03);
            };
        }

        return (instance, context) => System.Threading.Tasks.ValueTask.FromResult(x00(instance, context));
    }

    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static PacketMetadata GetPacketMetadata(System.Reflection.MethodInfo method)
    {
        return _attributeCache.GetOrAdd(method, static m =>
        {
            PacketMetadataBuilder builder = new()
            {
                // Core attributes – always populated from the method itself.
                Opcode = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketOpcodeAttribute>(m),
                Timeout = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketTimeoutAttribute>(m),
                Permission = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketPermissionAttribute>(m),
                Encryption = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketEncryptionAttribute>(m),
                RateLimit = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketRateLimitAttribute>(m),
                ConcurrencyLimit = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketConcurrencyLimitAttribute>(m)
            };

            // Let external providers extend or override metadata.
            foreach (IPacketMetadataProvider provider in PacketMetadataProviders.Providers)
            {
                provider.Populate(m, builder);
            }

            return builder.Build();
        });
    }

    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static string FormatHandlerInfo(string x00, ushort x01, System.Reflection.MethodInfo x02 = null, Type x03 = null)
    {
        string op = $"opcode=0x{x01:X4}";
        string ctrl = $"controller={x00}";
        string m = x02 is null ? "" : $" method={x02.Name}";
        string sig = x02 is null ? "" : $" sig=({string.Join(",", System.Linq.Enumerable
                                                                     .Select(x02
                                                                     .GetParameters(), p => p.ParameterType.Name))})->{x03?.Name ?? "void"}";

        return $"{op} {ctrl}{m}{sig}";
    }

    #endregion Private Methods
}
