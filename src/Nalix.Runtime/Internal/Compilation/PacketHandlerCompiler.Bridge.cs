// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Framework.Memory.Objects;

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
            bool withToken = kind == SignatureKind.ContextWithToken;
            Func<object?, ValueTask<object>> normalizer = CREATE_RESULT_NORMALIZER(method.ReturnType);
            MethodInfo bridgeMethod = GET_REQUIRED_METHOD(
                typeof(PacketHandlerCompiler<TController, TPacket>),
                nameof(INVOKE_CONTEXT_BRIDGE_ASYNC),
                BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(bridgePacketType);

            Func<MethodInfo, object, PacketContext<TPacket>, bool, Func<object?, ValueTask<object>>, ValueTask<object>> bridgeInvoker = bridgeMethod.CreateDelegate<
                Func<MethodInfo, object, PacketContext<TPacket>, bool,
                     Func<object?, ValueTask<object>>, ValueTask<object>>>();

            return (instance, context) =>
                bridgeInvoker(method, instance, context, withToken, normalizer);
        }
        catch (TypeInitializationException tie) when (tie.InnerException is InvalidOperationException ioe && ioe.Message.Contains("PacketRegistry is already built", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PacketRegistry was built too early. Make sure all packet assemblies are loaded " +
                "and handlers are registered BEFORE calling PacketRegistry.Build(). " +
                "See NetworkApplicationBuilder.", tie);
        }
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

        PacketContext<TConcretePacket> bridgedContext = s_pool.Get<PacketContext<TConcretePacket>>();

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
}
