// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Diagnostics;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Common.Messaging.Packets.Attributes;
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
                                .Debug($"[NW.{nameof(HandlerCompiler<,>)}:{nameof(CompileHandlers)}] scan controller={controllerType.Name}");

        // Get or compile all handler methods
        var compiledMethods = X04(controllerType);

        // Create the controller instance
        TController controllerInstance = factory();

        // CreateCatalog delegate descriptors
        PacketHandler<TPacket>[] descriptors = new PacketHandler<TPacket>[compiledMethods.Count];
        System.Int32 index = 0;

        foreach (var (opCode, compiledMethod) in compiledMethods)
        {
            var attributes = X01(compiledMethod.MethodInfo);

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
                                       $"found count={compiledMethods.Count} controller={controllerType.FullName} " +
                                       $"ops=[{firstOps}{(compiledMethods.Count > 6 ? ",..." : System.String.Empty)}]");

        return descriptors;
    }

    #region Private Methods

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Collections.Frozen.FrozenDictionary<System.UInt16, HandlerInvoker<TPacket>> X04(
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
            System.Collections.Generic.Dictionary<System.UInt16, HandlerInvoker<TPacket>> compiled = new(methods.Length);

            foreach (System.Reflection.MethodInfo method in methods)
            {
                PacketOpcodeAttribute opcodeAttr = System.Reflection.CustomAttributeExtensions
                    .GetCustomAttribute<PacketOpcodeAttribute>(method)!;

                if (compiled.ContainsKey(opcodeAttr.OpCode))
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Warn($"[NW.{nameof(HandlerCompiler<,>)}:Internal] dup-opcode " +
                                                  $"{X00(method.DeclaringType?.Name ?? "NONE", opcodeAttr.OpCode, method, method.ReturnType)}");

                    continue;
                }

                try
                {
                    HandlerInvoker<TPacket> compiledMethod = X03(method);
                    compiled[opcodeAttr.OpCode] = compiledMethod;

                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Trace($"[NW.{nameof(HandlerCompiler<,>)}:Internal] compiled " +
                                                   $"{X00(method.DeclaringType?.Name ?? "NONE", opcodeAttr.OpCode, method, method.ReturnType)}");
                }
                catch (System.Exception ex)
                {
                    System.String ___ = X00(method.DeclaringType?.Name ?? "NONE", opcodeAttr.OpCode, method, method.ReturnType);
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[NW.{nameof(HandlerCompiler<,>)}:Internal] " +
                                                   $"failed-compile {___} ex={ex.GetType().Name}", ex);
                }
            }

            return System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(compiled);
        }, methodInfos);
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static HandlerInvoker<TPacket> X03(System.Reflection.MethodInfo x22)
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
        System.Reflection.ParameterInfo[] parms = x22.GetParameters();

        if (parms.Length is not 2 and not 3)
        {
            throw new System.InvalidOperationException(
                $"Handler {x22.DeclaringType?.Name}.{x22.Name} must have 2 or 3 parameters " +
                "(packet, connection[, CancellationToken]). Found: {parms.Length}.");
        }

        System.Type x05 = parms[0].ParameterType;
        System.Type x06 = parms[1].ParameterType;

        if (!typeof(IPacket).IsAssignableFrom(x05))
        {
            throw new System.InvalidOperationException($"First parameter of {x22.Name} must implement IPacket. Found: {x05}.");
        }

        if (!typeof(IConnection).IsAssignableFrom(x06))
        {
            throw new System.InvalidOperationException($"Second parameter of {x22.Name} must implement IConnection. Found: {x06}.");
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

        System.Linq.Expressions.Expression x10 = x22.IsStatic
            ? System.Linq.Expressions.Expression.Call(x22, x09)
            : System.Linq.Expressions.Expression.Call(
              System.Linq.Expressions.Expression.Convert(x00, x22.DeclaringType!), x22, x09);

        System.Linq.Expressions.Expression x11 = x22.ReturnType == typeof(void)
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
            System.Type x13 = x22.IsStatic
                ? x22.CreateDelegateTypeForStatic()
                : x22.CreateDelegateTypeForInstance();

            System.Delegate x14 = x22.IsStatic
                ? x22.CreateDelegate(x13)
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
                System.Object x21 = x22.IsStatic
                    ? x22.Invoke(null, parms.Length == 2 ? [x18, x19] : [x18, x19, x17!])
                    : x22.Invoke(instance, parms.Length == 2 ? [x18, x19] : [x18, x19, x17!]);

                return x21;
            };
        }

        System.Func<System.Object, PacketContext<TPacket>,
            System.Threading.Tasks.ValueTask<System.Object>> x20 = X02(x12, x22.ReturnType);

        return new HandlerInvoker<TPacket>(x22, x22.ReturnType, x20);
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Func<System.Object, PacketContext<TPacket>, System.Threading.Tasks.ValueTask<System.Object>> X02(
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
    private static PacketMetadata X01(System.Reflection.MethodInfo x02)
    {
        return _attributeCache.GetOrAdd(x02, static x03 => new PacketMetadata(
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketOpcodeAttribute>(x03)!,
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketTimeoutAttribute>(x03),
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketPermissionAttribute>(x03),
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketEncryptionAttribute>(x03),
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketRateLimitAttribute>(x03),
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<PacketConcurrencyLimitAttribute>(x03)
        ));
    }

    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.String X00(System.String x00, System.UInt16 x01, System.Reflection.MethodInfo x02 = null, System.Type x03 = null)
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
