// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Nalix.Runtime.Dispatching;
namespace Nalix.Runtime.Internal.Compilation;

internal sealed partial class PacketHandlerCompiler<TController, TPacket>
{
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static Func<object, PacketContext<TPacket>, ValueTask<object>> WRAP_RETURN_TYPE(
        Func<object, PacketContext<TPacket>, object> x00,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type x01)
    {
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<object, ValueTask<object>> CREATE_TASK_CONVERTER(Type resultType)
    {
        MethodInfo method = GET_REQUIRED_METHOD(
            typeof(PacketHandlerCompiler<TController, TPacket>),
            nameof(AWAIT_TASK_RESULT_ASYNC),
            BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(resultType);

        return (Func<object, ValueTask<object>>)Delegate.CreateDelegate(typeof(Func<object, ValueTask<object>>), method);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<object, ValueTask<object>> CREATE_VALUE_TASK_CONVERTER(Type resultType)
    {
        MethodInfo method = GET_REQUIRED_METHOD(
            typeof(PacketHandlerCompiler<TController, TPacket>),
            nameof(AWAIT_VALUE_TASK_RESULT_ASYNC),
            BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(resultType);

        return (Func<object, ValueTask<object>>)Delegate.CreateDelegate(typeof(Func<object, ValueTask<object>>), method);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static async ValueTask<object> AWAIT_TASK_VOID_ASYNC(object result)
    {
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
        }

        return null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static async ValueTask<object> AWAIT_TASK_RESULT_ASYNC<TResult>(object result)
    {
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
        if (result is ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
        }

        return null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static async ValueTask<object> AWAIT_VALUE_TASK_RESULT_ASYNC<TResult>(object result)
    {
        if (result is ValueTask<TResult> valueTask)
        {
            TResult value = await valueTask.ConfigureAwait(false);
            return value!;
        }

        return result;
    }
}
