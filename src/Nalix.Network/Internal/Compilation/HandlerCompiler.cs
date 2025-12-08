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
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static PacketHandler<TPacket>[] CompileHandlers(System.Func<TController> factory)
    {
        var controllerType = typeof(TController);

        // Ensure controller has [PacketController] attribute
        PacketControllerAttribute controllerAttr = System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<PacketControllerAttribute>(controllerType)
            ?? throw new System.InvalidOperationException(
                $"Controller '{controllerType.Name}' is missing the [PacketController] attribute.");

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(HandlerCompiler<,>)}] " +
                                       $"scan controller={controllerType.Name}");

        // Get or compile all handler methods
        var compiledMethods = GetOrCompileMethodAccessors(controllerType);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(HandlerCompiler<,>)}] " +
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
                                .Debug($"[{nameof(HandlerCompiler<,>)}] " +
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
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Collections.Frozen.FrozenDictionary<System.UInt16, HandlerInvoker<TPacket>> GetOrCompileMethodAccessors(
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] System.Type controllerType)
    {
        // Get methods with [PacketOpcode] attribute
        System.Reflection.MethodInfo[] methodInfos = System.Linq.Enumerable.ToArray(
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
                                    .Debug($"[NW.{nameof(HandlerCompiler<,>)}] no-method controller={controllerType.Name}");
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(HandlerCompiler<,>)}] compile count={methodInfos.Length} controller={controllerType.Name}");

        return _compiledMethodCache.GetOrAdd(controllerType, static (_, methods) =>
        {
            System.Collections.Generic.Dictionary<System.UInt16, HandlerInvoker<TPacket>> compiled = new(methods.Length);

            foreach (System.Reflection.MethodInfo method in methods)
            {
                PacketOpcodeAttribute opcodeAttr = System.Reflection.CustomAttributeExtensions
                    .GetCustomAttribute<PacketOpcodeAttribute>(method)!;

                if (compiled.ContainsKey(opcodeAttr.OpCode))
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[NW.{nameof(HandlerCompiler<,>)}] dup-opcode " +
                                                  $"{X00(method.DeclaringType?.Name ?? "NONE", opcodeAttr.OpCode, method, method.ReturnType)}");

                    continue;
                }

                try
                {
                    HandlerInvoker<TPacket> compiledMethod = CompileMethodAccessor(method);
                    compiled[opcodeAttr.OpCode] = compiledMethod;

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[NW.{nameof(HandlerCompiler<,>)}] compiled " +
                                                   $"{X00(method.DeclaringType?.Name ?? "NONE", opcodeAttr.OpCode, method, method.ReturnType)}");
                }
                catch (System.Exception ex)
                {
                    System.String ___ = X00(method.DeclaringType?.Name ?? "NONE", opcodeAttr.OpCode, method, method.ReturnType);
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[NW.{nameof(HandlerCompiler<,>)}] " +
                                                   $"failed-compile {___} ex={ex.GetType().Name}", ex);
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
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static HandlerInvoker<TPacket> CompileMethodAccessor(System.Reflection.MethodInfo method)
    {
        System.Linq.Expressions.ParameterExpression x00 =
            System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "instance");

        System.Linq.Expressions.ParameterExpression x01 =
            System.Linq.Expressions.Expression.Parameter(typeof(PacketContext<TPacket>), "context");

        System.Linq.Expressions.MemberExpression x02 =
            System.Linq.Expressions.Expression.Property(x01, typeof(PacketContext<TPacket>).GetProperty(nameof(PacketContext<>.Packet))!);

        System.Linq.Expressions.MemberExpression x03 =
            System.Linq.Expressions.Expression.Property(x01, typeof(PacketContext<TPacket>).GetProperty(nameof(PacketContext<>.Connection))!);

        System.Linq.Expressions.MemberExpression x04 =
            System.Linq.Expressions.Expression.Property(x01, typeof(PacketContext<TPacket>).GetProperty(nameof(PacketContext<>.CancellationToken))!);


        // Get the actual parameter types of the method
        System.Reflection.ParameterInfo[] parms = method.GetParameters();

        if (parms.Length is not 2 and not 3)
        {
            throw new System.InvalidOperationException(
                $"Handler {method.DeclaringType?.Name}.{method.Name} must have 2 or 3 parameters " +
                "(packet, connection[, CancellationToken]). Found: {parms.Length}.");
        }

        System.Type x05 = parms[0].ParameterType;
        System.Type x06 = parms[1].ParameterType;

        if (!typeof(IPacket).IsAssignableFrom(x05))
        {
            throw new System.InvalidOperationException($"First parameter of {method.Name} must implement IPacket. Found: {x05}.");
        }

        if (!typeof(IConnection).IsAssignableFrom(x06))
        {
            throw new System.InvalidOperationException($"Second parameter of {method.Name} must implement IConnection. Found: {x06}.");
        }

        System.Linq.Expressions.Expression x07 = x05.IsAssignableFrom(typeof(TPacket))
            ? x02
            : System.Linq.Expressions.Expression.Convert(x02, x05);

        System.Linq.Expressions.Expression x08 = x06 == typeof(IConnection)
            ? x03
            : System.Linq.Expressions.Expression.Convert(x03, x06);

        System.Linq.Expressions.Expression[] x09 = parms.Length == 2
            ? [x07, x08]
            : [x07, x08, System.Linq.Expressions.Expression.Convert(x04, parms[2].ParameterType)];

        System.Linq.Expressions.Expression x10 = method.IsStatic
            ? System.Linq.Expressions.Expression.Call(method, x09)
            : System.Linq.Expressions.Expression.Call(
              System.Linq.Expressions.Expression.Convert(x00, method.DeclaringType!), method, x09);

        System.Linq.Expressions.Expression x11 = method.ReturnType == typeof(void)
            ? System.Linq.Expressions.Expression.Block(x10, System.Linq.Expressions.Expression.Constant(null, typeof(global::System.Object)))
            : System.Linq.Expressions.Expression.Convert(x10, typeof(global::System.Object));

        // Compile or create delegate depending on AOT support
        System.Func<System.Object, PacketContext<TPacket>, System.Object> x12;

        if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
        {
            // JIT-capable: compile IL
            x12 = System.Linq.Expressions.Expression
                    .Lambda<System.Func<System.Object, PacketContext<TPacket>, System.Object>>(x11, x00, x01)
                    .Compile();
        }
        else
        {
            // AOT fallback: CreateDelegate + tiny adapter
            System.Type x13 = method.IsStatic
                ? method.CreateDelegateTypeForStatic()
                : method.CreateDelegateTypeForInstance();

            System.Delegate x14 = method.IsStatic
                ? method.CreateDelegate(x13)
                : null; // instance-bound at x10 time

            x12 = (instance, context) =>
            {
                // materialize parameters
                System.Object x17 = null;
                System.Object x15 = context.Packet!;
                System.Object x16 = context.Connection;

                if (parms.Length == 3)
                {
                    x17 = context.CancellationToken;
                }

                // fast cast/convert
                System.Object x18 = x05.IsInstanceOfType(x15) ? x15 : System.Convert.ChangeType(x15, x05);
                System.Object x19 = x06.IsInstanceOfType(x16) ? x16 : System.Convert.ChangeType(x16, x06);

                // invoke
                System.Object x21 = method.IsStatic
                    ? method.Invoke(null, parms.Length == 2 ? [x18, x19] : [x18, x19, x17!])
                    : method.Invoke(instance, parms.Length == 2 ? [x18, x19] : [x18, x19, x17!]);

                return x21;
            };
        }

        System.Func<System.Object, PacketContext<TPacket>,
            System.Threading.Tasks.ValueTask<System.Object>> x20 = CreateAsyncWrapper(x12, method.ReturnType);

        return new HandlerInvoker<TPacket>(method, method.ReturnType, x20);
    }

    /// <summary>
    /// Creates an async-compatible wrapper for a compiled delegate,
    /// handling Task, ValueTask, and their generic variants.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Func<System.Object, PacketContext<TPacket>, System.Threading.Tasks.ValueTask<System.Object>> CreateAsyncWrapper(
        System.Func<System.Object, PacketContext<TPacket>, System.Object> x00,
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]System.Type x01)
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


    /// <summary>
    /// Retrieves all cached metadata attributes associated with a handler method.
    /// </summary>
    /// <param name="method">The method to scan.</param>
    /// <returns>The parsed packet metadata.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
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

    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.String X00(System.String controller, System.UInt16 opcode,
        System.Reflection.MethodInfo method = null, System.Type returnType = null)
    {
        System.String op = $"opcode=0x{opcode:X4}";
        System.String ctrl = $"controller={controller}";
        System.String m = method is null ? "" : $" method={method.Name}";
        System.String sig = method is null ? "" : $" sig=({System.String.Join(",", System.Linq.Enumerable
                                                                        .Select(method
                                                                        .GetParameters(), p => p.ParameterType.Name))})->{returnType?.Name ?? "void"}";

        return $"{op} {ctrl}{m}{sig}";
    }
}
