// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.Contracts;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;

using Nalix.Runtime.Dispatching;
namespace Nalix.Runtime.Internal.Compilation;

internal sealed partial class PacketHandlerCompiler<TController, TPacket>
{
    [Pure]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Func<object, PacketContext<TPacket>, ValueTask<object>> BUILD_CONTEXT_BRIDGE_INVOKER(MethodInfo method, ParameterInfo[] parms, SignatureKind kind)
    {
        try
        {
            Type bridgePacketType = parms[0].ParameterType.GetGenericArguments()[0];
            Func<object?, ValueTask<object>> normalizer = CREATE_RESULT_NORMALIZER(method.ReturnType);

            // Compile the handler delegate once at compilation/registration time.
            MethodInfo compileMethod = GET_REQUIRED_METHOD(
                typeof(PacketHandlerCompiler<TController, TPacket>),
                nameof(COMPILE_CONTEXT_HANDLER_DELEGATE),
                BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(bridgePacketType);

            object compiledHandler = compileMethod.Invoke(null, [method, kind])!;

            // Resolve the generic bridge method.
            MethodInfo bridgeMethod = GET_REQUIRED_METHOD(
                typeof(PacketHandlerCompiler<TController, TPacket>),
                nameof(INVOKE_CONTEXT_BRIDGE_ASYNC),
                BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(bridgePacketType);

            // Create the delegate to bridge method, passing the compiledHandler instead of MethodInfo method.
            Func<object, object, PacketContext<TPacket>, Func<object?, ValueTask<object>>, ValueTask<object>> bridgeInvoker =
                bridgeMethod.CreateDelegate<Func<object, object, PacketContext<TPacket>, Func<object?, ValueTask<object>>, ValueTask<object>>>();

            return (instance, context) =>
                bridgeInvoker(compiledHandler, instance, context, normalizer);
        }
        catch (TypeInitializationException tie) when (tie.InnerException is InvalidOperationException ioe && ioe.Message.Contains("PacketRegistry is already built", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PacketRegistry was built too early. Make sure all packet assemblies are loaded " +
                "and handlers are registered BEFORE calling PacketRegistry.Build(). " +
                "See NetworkApplicationBuilder.", tie);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<object, PacketContext<TConcretePacket>, object?> COMPILE_CONTEXT_HANDLER_DELEGATE<TConcretePacket>(MethodInfo method, SignatureKind kind)
        where TConcretePacket : IPacket
    {
        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            ParameterExpression instanceParam = Expression.Parameter(typeof(object), "instance");
            ParameterExpression contextParam = Expression.Parameter(typeof(PacketContext<TConcretePacket>), "context");

            Expression[] args;
            if (kind == SignatureKind.ContextWithToken)
            {
                PropertyInfo cancellationTokenProperty = typeof(PacketContext<TConcretePacket>).GetProperty(nameof(PacketContext<>.CancellationToken))!;
                args = [contextParam, Expression.Property(contextParam, cancellationTokenProperty)];
            }
            else
            {
                args = [contextParam];
            }

            Type declaringType = method.DeclaringType ?? throw new InvalidOperationException("Method is missing declaring type.");

            Expression callExpr = method.IsStatic
                ? Expression.Call(method, args)
                : Expression.Call(Expression.Convert(instanceParam, declaringType), method, args);

            Expression convertResult = method.ReturnType == typeof(void)
                ? Expression.Block(callExpr, Expression.Constant(null, typeof(object)))
                : Expression.Convert(callExpr, typeof(object));

            Expression<Func<object, PacketContext<TConcretePacket>, object?>> lambda = Expression.Lambda<Func<object, PacketContext<TConcretePacket>, object?>>(
                convertResult, instanceParam, contextParam);

            return lambda.Compile();
        }
        else
        {
            // NativeAOT fallback path: keep the exact reflection behavior.
            bool withToken = kind == SignatureKind.ContextWithToken;
            return (instance, context) =>
            {
                return withToken
                    ? method.Invoke(instance, [context, context.CancellationToken])
                    : method.Invoke(instance, [context]);
            };
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static async ValueTask<object> INVOKE_CONTEXT_BRIDGE_ASYNC<TConcretePacket>(
        object compiledHandlerObj, object instance, PacketContext<TPacket> context,
        Func<object?, ValueTask<object>> normalizer)
        where TConcretePacket : IPacket
    {
        if (context.Packet is not TConcretePacket concretePacket)
        {
            throw new InternalErrorException(
                $"Handler bridge expected packet type '{typeof(TConcretePacket).Name}' but received '{context.Packet?.GetType().Name ?? "null"}'.");
        }

        Func<object, PacketContext<TConcretePacket>, object?> compiledHandler = (Func<object, PacketContext<TConcretePacket>, object?>)compiledHandlerObj;
        PacketContext<TConcretePacket> bridgedContext = s_pool.Get<PacketContext<TConcretePacket>>();

        try
        {
            bridgedContext.Initialize(concretePacket, context.Connection, context.Attributes, context.IsReliable, context.CancellationToken);
            bridgedContext.SkipOutbound = context.SkipOutbound;

            object? result = compiledHandler(instance, bridgedContext);
            context.SkipOutbound = bridgedContext.SkipOutbound;

            if (context.SkipOutbound)
            {
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                    return null!;
                }
                if (result is ValueTask valueTask)
                {
                    await valueTask.ConfigureAwait(false);
                    return null!;
                }
            }

            object normalized = await normalizer(result).ConfigureAwait(false);
            return normalized;
        }
        finally
        {
            bridgedContext.Return();
        }
    }
}
