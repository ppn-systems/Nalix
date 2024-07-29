// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Framework.Injection;
using Nalix.Network.Dispatch;
using Nalix.Network.Dispatch.Delegates;
using Nalix.Network.Internal.Extensions;

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
            System.UInt16, HandlerInvoker<TPacket>>> _compiledMethodCache = new();

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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static PacketHandler<TPacket>[] CompileHandlers(System.Func<TController> factory)
    {
        var controllerType = typeof(TController);

        // Ensure controller has [PacketController] attribute
        PacketControllerAttribute controllerAttr = System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<PacketControllerAttribute>(controllerType)
            ?? throw new System.InvalidOperationException(
                $"Controller '{controllerType.Name}' is missing the [PacketController] attribute.");

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[{nameof(HandlerCompiler<TController, TPacket>)}] " +
                                      $"scan controller={controllerType.Name}");

        // Get or compile all handler methods
        var compiledMethods = GetOrCompileMethodAccessors(controllerType);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(HandlerCompiler<TController, TPacket>)}] " +
                                       $"found count={compiledMethods.Count}");

        // Create the controller instance
        TController controllerInstance = factory();

        // CreateCatalog delegate descriptors
        PacketHandler<TPacket>[] descriptors = new PacketHandler<TPacket>[compiledMethods.Count];
        System.Int32 index = 0;

        foreach (var (opCode, compiledMethod) in compiledMethods)
        {
            var attributes = GetCachedAttributes(compiledMethod.MethodInfo);

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
                                .Debug($"[{nameof(HandlerCompiler<TController, TPacket>)}] " +
                                       $"found count={compiledMethods.Count} " +
                                       $"controller={controllerType.FullName} " +
                                       $"ops=[{firstOps}{(compiledMethods.Count > 6 ? ",..." : System.String.Empty)}]");

        return descriptors;
    }

    /// <summary>
    /// Gets or compiles method handlers for a controller type.
    /// </summary>
    /// <param name="controllerType">The controller type.</param>
    /// <returns>A frozen dictionary of compiled handler delegates indexed by opcode.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Collections.Frozen.FrozenDictionary<System.UInt16, HandlerInvoker<TPacket>>
        GetOrCompileMethodAccessors(
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)]
        System.Type controllerType)
    {
        // Get methods with [PacketOpcode] attribute
        var methodInfos = System.Linq.Enumerable.ToArray(
            System.Linq.Enumerable.Where(
                controllerType.GetMethods(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static
                ),
                m => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketOpcodeAttribute>(m) is not null));

        if (methodInfos.Length == 0)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(HandlerCompiler<TController, TPacket>)}] " +
                                          $"no-method controller={controllerType.Name}");
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(HandlerCompiler<TController, TPacket>)}] " +
                                       $"compile count={methodInfos.Length} controller={controllerType.Name}");

        return _compiledMethodCache.GetOrAdd(controllerType, static (_, methods) =>
        {
            System.Collections.Generic.Dictionary<System.UInt16, HandlerInvoker<TPacket>> compiled = new(methods.Length);

            foreach (var method in methods)
            {
                var opcodeAttr = System.Reflection.CustomAttributeExtensions
                    .GetCustomAttribute<PacketOpcodeAttribute>(method)!;

                if (compiled.ContainsKey(opcodeAttr.OpCode))
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[{nameof(HandlerCompiler<TController, TPacket>)}] dup-opcode " +
                                                  $"{Ctx(method.DeclaringType?.Name ?? "NONE", opcodeAttr.OpCode, method, method.ReturnType)}");

                    continue;
                }

                try
                {
                    HandlerInvoker<TPacket> compiledMethod = CompileMethodAccessor(method);
                    compiled[opcodeAttr.OpCode] = compiledMethod;

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[{nameof(HandlerCompiler<TController, TPacket>)}] compiled " +
                                                   $"{Ctx(method.DeclaringType?.Name ?? "NONE", opcodeAttr.OpCode, method, method.ReturnType)}");
                }
                catch (System.Exception ex)
                {
                    System.String ctx = Ctx(method.DeclaringType?.Name ?? "NONE", opcodeAttr.OpCode, method, method.ReturnType);
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[{nameof(HandlerCompiler<TController, TPacket>)}] " +
                                                   $"failed-compile {ctx} ex={ex.GetType().Name}", ex);
                }
            }

            return System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(compiled);
        }, methodInfos);
    }

    /// <summary>
    /// Compiles a method accessor into a high-performance delegate using expression trees.
    /// </summary>
    /// <param name="method">The method to compile.</param>
    /// <returns>A compiled handler delegate.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static HandlerInvoker<TPacket> CompileMethodAccessor(System.Reflection.MethodInfo method)
    {
        var instanceParam = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "instance");
        var contextParam = System.Linq.Expressions.Expression.Parameter(typeof(PacketContext<TPacket>), "context");

        var pktProp = System.Linq.Expressions.Expression.Property(contextParam, typeof(PacketContext<TPacket>)
                                                        .GetProperty(nameof(PacketContext<TPacket>.Packet))!);
        var connProp = System.Linq.Expressions.Expression.Property(contextParam, typeof(PacketContext<TPacket>)
                                                         .GetProperty(nameof(PacketContext<TPacket>.Connection))!);
        var ctProp = System.Linq.Expressions.Expression.Property(contextParam, typeof(PacketContext<TPacket>)
                                                       .GetProperty(nameof(PacketContext<TPacket>.CancellationToken))!);


        // Get the actual parameter types of the method
        var parms = method.GetParameters();

        if (parms.Length is not 2 and not 3)
        {
            throw new System.InvalidOperationException(
                $"Handler {method.DeclaringType?.Name}.{method.Name} must have 2 or 3 parameters " +
                "(packet, connection[, CancellationToken]). Found: {parms.Length}.");
        }
        var pktArgType = parms[0].ParameterType;
        var connArgType = parms[1].ParameterType;

        if (!typeof(IPacket).IsAssignableFrom(pktArgType))
        {
            throw new System.InvalidOperationException($"First parameter of {method.Name} must implement IPacket. Found: {pktArgType}.");
        }

        if (!typeof(IConnection).IsAssignableFrom(connArgType))
        {
            throw new System.InvalidOperationException($"Second parameter of {method.Name} must implement IConnection. Found: {connArgType}.");
        }

        var pktArg = pktArgType.IsAssignableFrom(typeof(TPacket)) ? (System.Linq.Expressions.Expression)pktProp
                     : System.Linq.Expressions.Expression.Convert(pktProp, pktArgType);

        var connArg = connArgType == typeof(IConnection) ? (System.Linq.Expressions.Expression)connProp
                     : System.Linq.Expressions.Expression.Convert(connProp, connArgType);

        var args = parms.Length == 2
            ? [pktArg, connArg]
            : new[] { pktArg, connArg, System.Linq.Expressions.Expression.Convert(ctProp, parms[2].ParameterType) };

        System.Linq.Expressions.Expression call = method.IsStatic
            ? System.Linq.Expressions.Expression.Call(method, args)
            : System.Linq.Expressions.Expression.Call(
                System.Linq.Expressions.Expression.Convert(instanceParam, method.DeclaringType!), method, args);

        System.Linq.Expressions.Expression body = method.ReturnType == typeof(void)
            ? System.Linq.Expressions.Expression.Block(call, System.Linq.Expressions.Expression.Constant(null, typeof(global::System.Object)))
            : System.Linq.Expressions.Expression.Convert(call, typeof(global::System.Object));

        // Compile or create delegate depending on AOT support
        System.Func<System.Object, PacketContext<TPacket>, System.Object?> sync;

        if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
        {
            // JIT-capable: compile IL
            sync = System.Linq.Expressions.Expression
                .Lambda<System.Func<System.Object, PacketContext<TPacket>, System.Object?>>(body, instanceParam, contextParam)
                .Compile();
        }
        else
        {
            // AOT fallback: CreateDelegate + tiny adapter
            var openDelegateType = method.IsStatic
                ? method.CreateDelegateTypeForStatic()
                : method.CreateDelegateTypeForInstance();

            var dlg = method.IsStatic
                ? method.CreateDelegate(openDelegateType)
                : null; // instance-bound at call time

            sync = (instance, context) =>
            {
                // materialize parameters
                var p0 = ((System.Object?)context.Packet)!;
                var p1 = (System.Object)context.Connection;
                System.Object? p2 = null;

                if (parms.Length == 3)
                {
                    p2 = context.CancellationToken;
                }

                // fast cast/convert
                var a0 = pktArgType.IsInstanceOfType(p0) ? p0 : System.Convert.ChangeType(p0, pktArgType);
                var a1 = connArgType.IsInstanceOfType(p1) ? p1 : System.Convert.ChangeType(p1, connArgType);

                // invoke
                System.Object? result = method.IsStatic
                    ? method.Invoke(null, parms.Length == 2 ? [a0, a1] : [a0, a1, p2!])
                    : method.Invoke(instance, parms.Length == 2 ? [a0, a1] : [a0, a1, p2!]);

                return result;
            };
        }

        var asyncWrapper = CreateAsyncWrapper(sync, method.ReturnType);
        return new HandlerInvoker<TPacket>(method, method.ReturnType, asyncWrapper);
    }

    /// <summary>
    /// Creates an async-compatible wrapper for a compiled delegate,
    /// handling Task, ValueTask, and their generic variants.
    /// </summary>
    private static System.Func<System.Object, PacketContext<TPacket>, System.Threading.Tasks.ValueTask<System.Object?>> CreateAsyncWrapper(
        System.Func<System.Object, PacketContext<TPacket>, System.Object?> syncDelegate,
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
        System.Type returnType)
    {
        if (returnType == typeof(System.Threading.Tasks.Task))
        {
            return async (instance, context) =>
            {
                if (syncDelegate(instance, context) is System.Threading.Tasks.Task t)
                {
                    await t.ConfigureAwait(false);
                    return null;
                }
                return null;
            };
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>))
        {
            // Cache Result getter at compile-time for this returnType
            var resultProp = returnType.GetProperty("Result")!;
            return async (instance, context) =>
            {
                var r = syncDelegate(instance, context);
                if (r is System.Threading.Tasks.Task t)
                {
                    await t.ConfigureAwait(false);
                    return resultProp.GetValue(t);
                }
                return r;
            };
        }

        if (returnType == typeof(System.Threading.Tasks.ValueTask))
        {
            // Call .GetAwaiter().GetResult() without allocations
            var getAwaiter = typeof(System.Threading.Tasks.ValueTask)
                .GetMethod("GetAwaiter", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!;

            var awaiterIsCompleted = getAwaiter.ReturnType.GetProperty("IsCompleted")!;
            var awaiterGetResult = getAwaiter.ReturnType.GetMethod("GetResult")!;

            return async (instance, context) =>
            {
                var r = syncDelegate(instance, context);
                if (r is System.Threading.Tasks.ValueTask vt)
                {
                    // prefer await: lets the compiler pick optimal path
                    await vt.ConfigureAwait(false);
                    return null;
                }
                return null;
            };
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.ValueTask<>))
        {
            // Build a converter: ValueTask<T> -> Task<T> once, then await Task<T> (no dynamic)
            var vtResultProp = returnType.GetProperty("Result"); // exists but only valid if completed
            var asTaskMethod = returnType.GetMethod("AsTask", System.Type.EmptyTypes)!; // ValueTask<T>.AsTask()

            return async (instance, context) =>
            {
                var r = syncDelegate(instance, context);
                if (r is null)
                {
                    return null;
                }

                // call AsTask() via reflection once per wrapper
                var taskObj = asTaskMethod.Invoke(r, null)!; // Task<T>
                var task = (System.Threading.Tasks.Task)taskObj;
                await task.ConfigureAwait(false);

                // read Task<T>.Result once completed
                var taskResultProp = taskObj.GetType().GetProperty("Result")!;
                return taskResultProp.GetValue(taskObj);
            };
        }

        return (instance, context) =>
            System.Threading.Tasks.ValueTask.FromResult(syncDelegate(instance, context));
    }


    /// <summary>
    /// Retrieves all cached metadata attributes associated with a handler method.
    /// </summary>
    /// <param name="method">The method to scan.</param>
    /// <returns>The parsed packet metadata.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static PacketMetadata GetCachedAttributes(System.Reflection.MethodInfo method)
    {
        return _attributeCache.GetOrAdd(method, static m => new PacketMetadata(
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketOpcodeAttribute>(m)!,
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketTimeoutAttribute>(m),
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketPermissionAttribute>(m),
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketEncryptionAttribute>(m),
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketRateLimitAttribute>(m),
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketConcurrencyLimitAttribute>(m)
        ));
    }

    private static System.String Ctx(
        System.String controller, System.UInt16 opcode,
        System.Reflection.MethodInfo? method = null, System.Type? returnType = null)
    {
        var op = $"opcode=0x{opcode:X4}";
        var ctrl = $"controller={controller}";
        var m = method is null ? "" : $" method={method.Name}";
        var sig = method is null ? "" : $" sig=({System.String.Join(",", System.Linq.Enumerable
                                                              .Select(method
                                                              .GetParameters(), p => p.ParameterType.Name))})->{returnType?.Name ?? "void"}";

        return $"{op} {ctrl}{m}{sig}";
    }
}
