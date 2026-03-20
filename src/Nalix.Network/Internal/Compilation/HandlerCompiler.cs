// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Abstractions;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Packets.Attributes;
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
        System.Type, System.Collections.Frozen.FrozenDictionary<
            System.UInt16, CompiledHandler<TPacket>>> _compiledMethodCache = new();

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static PacketHandler<TPacket>[] CompileHandlers(System.Func<TController> factory)
    {
        var controllerType = typeof(TController);

        // Ensure controller has [PacketController] attribute
        PacketControllerAttribute controllerAttr = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketControllerAttribute>(controllerType)
            ?? throw new System.InvalidOperationException($"Controller '{controllerType.Name}' is missing the [PacketController] attribute.");

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(HandlerCompiler<,>)}:{nameof(CompileHandlers)}] scan controller={controllerType.Name}");

        // Get or compile all handler methods
        var compiledMethods = CompileControllerHandlers(controllerType);

        // Create the controller instance
        TController controllerInstance = factory();

        // CreateCatalog delegate descriptors
        PacketHandler<TPacket>[] descriptors = new PacketHandler<TPacket>[compiledMethods.Count];
        System.Int32 index = 0;

        foreach (var (opCode, compiledMethod) in compiledMethods)
        {
            var attributes = GetPacketMetadata(compiledMethod.MethodInfo);

            descriptors[index++] = new PacketHandler<TPacket>(
                opCode,
                attributes,
                controllerInstance,
                compiledMethod.MethodInfo,
                compiledMethod.ReturnType,
                compiledMethod.CompiledInvoker);
        }

        System.String firstOps = System.String.Join(",", System.Linq.Enumerable
                                              .Select(System.Linq.Enumerable
                                              .Take(compiledMethods.Keys, 6), o => $"0x{o:X4}"));

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(HandlerCompiler<,>)}:{nameof(CompileHandlers)}] " +
                                       $"found count={compiledMethods.Count} controller={controllerType.FullName} ops=[{firstOps}{(compiledMethods.Count > 6 ? ",..." : System.String.Empty)}]");

        return descriptors;
    }

    #region Private Methods

    /// <summary>
    /// Describes the recognized parameter signature of a handler method.
    /// </summary>
    private enum SignatureKind
    {
        /// <summary>(TPacket, IConnection)</summary>
        LegacyNoToken,

        /// <summary>(TPacket, IConnection, CancellationToken)</summary>
        LegacyWithToken,

        /// <summary>(PacketContext&lt;TPacket&gt;)</summary>
        ContextOnly,

        /// <summary>(PacketContext&lt;TPacket&gt;, CancellationToken)</summary>
        ContextWithToken,
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Collections.Frozen.FrozenDictionary<System.UInt16, CompiledHandler<TPacket>> CompileControllerHandlers(
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] System.Type x03)
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
            System.Collections.Generic.Dictionary<System.UInt16, CompiledHandler<TPacket>> compiled = new(methods.Length);

            foreach (System.Reflection.MethodInfo method in methods)
            {
                PacketOpcodeAttribute opcodeAttr = System.Reflection.CustomAttributeExtensions
                    .GetCustomAttribute<PacketOpcodeAttribute>(method)!;

                if (compiled.ContainsKey(opcodeAttr.OpCode))
                {
                    System.String x01 = FormatHandlerInfo(method.DeclaringType?.Name ?? "NONE", opcodeAttr.OpCode, method, method.ReturnType);

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[NW.{nameof(HandlerCompiler<,>)}:Internal] dup-opcode {x01}");

                    continue;
                }

                try
                {
                    CompiledHandler<TPacket> compiledMethod = CompileHandlerMethod(method);
                    compiled[opcodeAttr.OpCode] = compiledMethod;

                    System.String x01 = FormatHandlerInfo(method.DeclaringType?.Name ?? "NONE", opcodeAttr.OpCode, method, method.ReturnType);

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[NW.{nameof(HandlerCompiler<,>)}:Internal] compiled {x01}");
                }
                catch (System.Exception ex)
                {
                    System.String x01 = FormatHandlerInfo(method.DeclaringType?.Name ?? "NONE", opcodeAttr.OpCode, method, method.ReturnType);

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
            System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "instance");

        System.Linq.Expressions.ParameterExpression x01 =
            System.Linq.Expressions.Expression.Parameter(typeof(PacketContext<TPacket>), "context");

        System.Type contextType = typeof(PacketContext<TPacket>);

        System.Linq.Expressions.MemberExpression x02 =
            System.Linq.Expressions.Expression.Property(x01, contextType.GetProperty(nameof(PacketContext<>.Packet))!);

        System.Linq.Expressions.MemberExpression x03 =
            System.Linq.Expressions.Expression.Property(x01, contextType.GetProperty(nameof(PacketContext<>.Connection))!);

        System.Linq.Expressions.MemberExpression x04 =
            System.Linq.Expressions.Expression.Property(x01, contextType.GetProperty(nameof(PacketContext<>.CancellationToken))!);

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
        // Build the argument list fed to the actual method call expression.
        // -------------------------------------------------------------------
        System.Linq.Expressions.Expression[] x09 = BuildArgExpressions(kind, parms, x01, x02, x03, x04);

        System.Linq.Expressions.Expression x10 = x22.IsStatic
            ? System.Linq.Expressions.Expression.Call(x22, x09)
            : System.Linq.Expressions.Expression.Call(
                System.Linq.Expressions.Expression.Convert(x00, x22.DeclaringType!), x22, x09);

        System.Linq.Expressions.Expression x11 = x22.ReturnType == typeof(void)
            ? System.Linq.Expressions.Expression.Block(x10, System.Linq.Expressions.Expression.Constant(null, typeof(System.Object)))
            : System.Linq.Expressions.Expression.Convert(x10, typeof(System.Object));

        // -------------------------------------------------------------------
        // Compile or fall back to reflection for AOT environments.
        // -------------------------------------------------------------------
        System.Func<System.Object, PacketContext<TPacket>, System.Object> x12;

        if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
        {
            x12 = System.Linq.Expressions.Expression
                    .Lambda<System.Func<System.Object, PacketContext<TPacket>, System.Object>>(x11, x00, x01)
                    .Compile();
        }
        else
        {
            // AOT fallback — build invoke args at call-time from context fields.
            x12 = BuildAotInvoker(x22, parms, kind);
        }

        System.Func<System.Object, PacketContext<TPacket>,
            System.Threading.Tasks.ValueTask<System.Object>> x20 = WrapReturnType(x12, x22.ReturnType);

        return new CompiledHandler<TPacket>(x22, x22.ReturnType, x20);
    }

    /// <summary>
    /// Determines the <see cref="SignatureKind"/> of a handler method.
    /// Throws <see cref="System.InvalidOperationException"/> for unrecognised signatures.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static SignatureKind ResolveSignatureKind(
        System.Reflection.MethodInfo method,
        System.Reflection.ParameterInfo[] parms)
    {
        // ---- new-style: first param is PacketContext<TPacket> ----
        if (parms.Length >= 1 && parms[0].ParameterType == typeof(PacketContext<TPacket>))
        {
            return parms.Length == 1
            ? SignatureKind.ContextOnly
            : parms.Length == 2 && parms[1].ParameterType == typeof(System.Threading.CancellationToken)
            ? SignatureKind.ContextWithToken
            : throw new System.InvalidOperationException(
            $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
            "when the first parameter is PacketContext<TPacket>, " +
            "the only valid second parameter is CancellationToken. " +
            $"Found {parms.Length} parameter(s).");
        }

        // ---- legacy-style: first param must implement IPacket ----
        return parms.Length >= 1 && typeof(IPacket).IsAssignableFrom(parms[0].ParameterType)
        ? parms.Length < 2 || !typeof(IConnection).IsAssignableFrom(parms[1].ParameterType)
        ? throw new System.InvalidOperationException(
        $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
        "legacy signature requires (TPacket, IConnection[, CancellationToken]). " +
        "Second parameter must implement IConnection.")
        : parms.Length == 2
        ? SignatureKind.LegacyNoToken
        : parms.Length == 3 && parms[2].ParameterType == typeof(System.Threading.CancellationToken)
        ? SignatureKind.LegacyWithToken
        : throw new System.InvalidOperationException(
        $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
        "legacy signature only supports 2 or 3 parameters " +
        $"(TPacket, IConnection[, CancellationToken]). Found {parms.Length}.")
        : throw new System.InvalidOperationException(
        $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
        "unrecognised signature. " +
        "Supported forms: " +
        "(TPacket, IConnection), " +
        "(TPacket, IConnection, CancellationToken), " +
        "(PacketContext<TPacket>), " +
        "(PacketContext<TPacket>, CancellationToken).");
    }

    /// <summary>
    /// Builds the argument expression array for the compiled method-call expression.
    /// </summary>
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
                // Pass the context object directly — no conversion needed.
                return [context];

            case SignatureKind.ContextWithToken:
                // (PacketContext<TPacket>, CancellationToken)
                // CT comes from context.CancellationToken so both refer to the same token.
                return [context, ctExpr];

            case SignatureKind.LegacyNoToken:
                {
                    System.Type packetType = parms[0].ParameterType;
                    System.Type connType = parms[1].ParameterType;

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
                    System.Type packetType = parms[0].ParameterType;
                    System.Type connType = parms[1].ParameterType;

                    System.Linq.Expressions.Expression pktArg = packetType.IsAssignableFrom(typeof(TPacket))
                        ? packetExpr
                        : System.Linq.Expressions.Expression.Convert(packetExpr, packetType);

                    System.Linq.Expressions.Expression connArg = connType == typeof(IConnection)
                        ? connectionExpr
                        : System.Linq.Expressions.Expression.Convert(connectionExpr, connType);

                    return [pktArg, connArg, ctExpr];
                }

            default:
                throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static System.Func<System.Object, PacketContext<TPacket>, System.Object> BuildAotInvoker(
        System.Reflection.MethodInfo method,
        System.Reflection.ParameterInfo[] parms,
        SignatureKind kind)
    {
        return kind switch
        {
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
                    System.Type p0 = parms[0].ParameterType;
                    System.Type p1 = parms[1].ParameterType;

                    System.Object pkt = p0.IsInstanceOfType(context.Packet) ? context.Packet : System.Convert.ChangeType(context.Packet, p0);
                    System.Object conn = p1.IsInstanceOfType(context.Connection) ? context.Connection : System.Convert.ChangeType(context.Connection, p1);

                    return method.IsStatic
                        ? method.Invoke(null, [pkt, conn])
                        : method.Invoke(instance, [pkt, conn]);
                }
            ,

            SignatureKind.LegacyWithToken =>
                (instance, context) =>
                {
                    System.Type p0 = parms[0].ParameterType;
                    System.Type p1 = parms[1].ParameterType;

                    System.Object pkt = p0.IsInstanceOfType(context.Packet) ? context.Packet : System.Convert.ChangeType(context.Packet, p0);
                    System.Object conn = p1.IsInstanceOfType(context.Connection) ? context.Connection : System.Convert.ChangeType(context.Connection, p1);

                    return method.IsStatic
                        ? method.Invoke(null, [pkt, conn, context.CancellationToken])
                        : method.Invoke(instance, [pkt, conn, context.CancellationToken]);
                }
            ,

            _ => throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Func<System.Object, PacketContext<TPacket>, System.Threading.Tasks.ValueTask<System.Object>> WrapReturnType(
        System.Func<System.Object, PacketContext<TPacket>, System.Object> x00,
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] System.Type x01)
    {
        if (x01 == typeof(System.Threading.Tasks.Task))
        {
            return async (instance, context) =>
            {
                if (x00(instance, context) is System.Threading.Tasks.Task t)
                {
                    await t.ConfigureAwait(false);
                    return null;
                }
                return null;
            };
        }

        if (x01.IsGenericType && x01.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>))
        {
            // Cache Result getter at compile-time for this x01
            System.Reflection.PropertyInfo x02 = x01.GetProperty("Result")!;
            return async (instance, context) =>
            {
                System.Object r = x00(instance, context);
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
                .GetMethod("GetAwaiter", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!;

            System.Reflection.PropertyInfo x02 = getAwaiter.ReturnType.GetProperty("IsCompleted")!;
            System.Reflection.MethodInfo x03 = getAwaiter.ReturnType.GetMethod("GetResult")!;

            return async (instance, context) =>
            {
                System.Object r = x00(instance, context);
                if (r is System.Threading.Tasks.ValueTask vt)
                {
                    // prefer await: lets the compiler pick optimal path
                    await vt.ConfigureAwait(false);
                    return null;
                }
                return null;
            };
        }

        if (x01.IsGenericType && x01.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.ValueTask<>))
        {
            // Build a converter: ValueTask<T> -> Task<T> once, then await Task<T> (no dynamic)
            System.Reflection.PropertyInfo x06 = x01.GetProperty("Result"); // exists but only valid if completed
            System.Reflection.MethodInfo x07 = x01.GetMethod("AsTask", System.Type.EmptyTypes)!; // ValueTask<T>.AsTask()

            return async (instance, context) =>
            {
                System.Object r = x00(instance, context);
                if (r is null)
                {
                    return null;
                }

                // x10 AsTask() via reflection once per wrapper
                System.Object x03 = x07.Invoke(r, null)!; // Task<T>
                System.Threading.Tasks.Task x04 = (System.Threading.Tasks.Task)x03;
                await x04.ConfigureAwait(false);

                // read Task<T>.Result once completed
                System.Reflection.PropertyInfo x05 = x03.GetType().GetProperty("Result")!;
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
                Opcode = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketOpcodeAttribute>(m)!,
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
    private static System.String FormatHandlerInfo(System.String x00, System.UInt16 x01, System.Reflection.MethodInfo x02 = null, System.Type x03 = null)
    {
        System.String op = $"opcode=0x{x01:X4}";
        System.String ctrl = $"controller={x00}";
        System.String m = x02 is null ? "" : $" method={x02.Name}";
        System.String sig = x02 is null ? "" : $" sig=({System.String.Join(",", System.Linq.Enumerable
                                                                     .Select(x02
                                                                     .GetParameters(), p => p.ParameterType.Name))})->{x03?.Name ?? "void"}";

        return $"{op} {ctrl}{m}{sig}";
    }

    #endregion Private Methods
}