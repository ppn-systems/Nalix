// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Runtime.Internal.Results.Memory;
using Nalix.Runtime.Internal.Results.Packet;
using Nalix.Runtime.Internal.Results.Primitives;
using Nalix.Runtime.Internal.Results.Task;
using Nalix.Runtime.Internal.Results.Void;

namespace Nalix.Runtime.Internal.Results;

/// <summary>
/// A zero-allocation factory responsible for returning the appropriate
/// IReturnHandler{TPacket} instance based on a method's return type.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type used for handling communication. Must implement IPacket.
/// </typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class ReturnTypeHandlerFactory<TPacket> where TPacket : IPacket
{
    private static readonly System.Collections.Frozen.FrozenDictionary<Type, IReturnHandler<TPacket>> s_handlers;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, IReturnHandler<TPacket>> s_dynamicHandlers = new();

    static ReturnTypeHandlerFactory()
    {
        // Ensure the factory is initialized with the correct packet type.
        if (!typeof(TPacket).IsAssignableTo(typeof(IPacket)))
        {
            throw new ArgumentException($"TPacket must implement {nameof(IPacket)}.");
        }

        s_handlers = CreateReturnTypeHandlerMap();
    }

    /// <summary>
    /// Returns the cached handler for a specific return type.
    /// If the type is not part of the base map, the factory lazily builds a
    /// wrapper handler for <c>Task&lt;T&gt;</c> / <c>ValueTask&lt;T&gt;</c> or an
    /// unsupported-type handler when the return type is not recognized.
    /// </summary>
    /// <param name="returnType"></param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static IReturnHandler<TPacket> ResolveHandler(Type returnType)
    {
        if (s_handlers.TryGetValue(returnType, out IReturnHandler<TPacket>? handler)
            && handler is not null)
        {
            return handler;
        }

        return s_dynamicHandlers.GetOrAdd(returnType, static type => CreateDynamicHandler(type));
    }

    /// <summary>
    /// Creates the immutable base handler map for the common return types.
    /// These handlers cover the fast path and avoid reflection for the built-in
    /// return shapes that appear most often in packet handlers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static System.Collections.Frozen.FrozenDictionary<Type, IReturnHandler<TPacket>> CreateReturnTypeHandlerMap()
    {
        Dictionary<Type, IReturnHandler<TPacket>> handlers = new()
        {
            [typeof(void)] = new VoidReturnHandler<TPacket>(),
            [typeof(TPacket)] = new PacketReturnHandler<TPacket>(),
            [typeof(byte[])] = new ByteArrayReturnHandler<TPacket>(),
            [typeof(Memory<byte>)] = new MemoryReturnHandler<TPacket>(),
            [typeof(System.Threading.Tasks.Task)] = new TaskVoidReturnHandler<TPacket>(),
            [typeof(ValueTask)] = new ValueTaskVoidReturnHandler<TPacket>(),
            [typeof(ReadOnlyMemory<byte>)] = new ReadOnlyMemoryReturnHandler<TPacket>()
        };

        return System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(handlers);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static IReturnHandler<TPacket> CreateTaskWrapperHandler(IReturnHandler<TPacket> innerHandler, Type resultType)
    {
        Type handlerType = typeof(TaskReturnHandler<,>).MakeGenericType(typeof(TPacket), resultType);
        return (IReturnHandler<TPacket>)(Activator.CreateInstance(handlerType, innerHandler)
            ?? throw new InternalErrorException($"Failed to create return handler for '{handlerType.FullName}'."));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static IReturnHandler<TPacket> CreateValueTaskWrapperHandler(IReturnHandler<TPacket> innerHandler, Type resultType)
    {
        Type handlerType = typeof(ValueTaskReturnHandler<,>).MakeGenericType(typeof(TPacket), resultType);
        return (IReturnHandler<TPacket>)(Activator.CreateInstance(handlerType, innerHandler)
            ?? throw new InternalErrorException($"Failed to create return handler for '{handlerType.FullName}'."));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static IReturnHandler<TPacket> CreateDynamicHandler(Type returnType)
    {
        if (returnType.IsGenericType)
        {
            Type genericArg = returnType.GetGenericArguments()[0];
            Type genericType = returnType.GetGenericTypeDefinition();

            if (genericType == typeof(Task<>))
            {
                // Task<T> is handled by first resolving the inner handler for T and
                // then wrapping it in an awaitable adapter that unwraps the Task.
                IReturnHandler<TPacket> innerHandler = ResolveHandler(genericArg);
                return CreateTaskWrapperHandler(innerHandler, genericArg);
            }

            if (genericType == typeof(ValueTask<>))
            {
                // Same wrapper pattern for ValueTask<T>: unwrap the result after
                // awaiting and feed it through the handler selected for T.
                IReturnHandler<TPacket> innerHandler = ResolveHandler(genericArg);
                return CreateValueTaskWrapperHandler(innerHandler, genericArg);
            }
        }

        return new UnsupportedReturnHandler<TPacket>(returnType);
    }
}
