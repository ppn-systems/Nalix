// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Abstractions;
using Nalix.Network.Dispatch.Results.Memory;
using Nalix.Network.Dispatch.Results.Packet;
using Nalix.Network.Dispatch.Results.Primitives;
using Nalix.Network.Dispatch.Results.Task;
using Nalix.Network.Dispatch.Results.Void;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Dispatch.Results;

/// <summary>
/// A zero-allocation factory responsible for returning the appropriate
/// IReturnHandler{TPacket} instance based on a method's return type.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type used for handling communication. Must implement IPacket.
/// </typeparam>
internal static class ReturnTypeHandlerFactory<TPacket> where TPacket : IPacket
{
    private static readonly System.Collections.Frozen.FrozenDictionary<System.Type, IReturnHandler<TPacket>> _handlers;

    static ReturnTypeHandlerFactory()
    {
        // Ensure the factory is initialized with the correct packet type.
        if (!typeof(TPacket).IsAssignableTo(typeof(IPacket)))
        {
            throw new System.ArgumentException(
                $"TPacket must implement {nameof(IPacket)}.");
        }

        _handlers = CreateHandlers();
    }

    /// <summary>
    /// Get handler cho specific return type.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static IReturnHandler<TPacket> GetHandler(
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.Type returnType)
    {
        if (_handlers.TryGetValue(returnType, out IReturnHandler<TPacket> handler))
        {
            return handler;
        }

        // Handle generic Task<T> and ValueTask<T>
        if (returnType.IsGenericType)
        {
            System.Type genericArg = returnType.GetGenericArguments()[0];
            System.Type genericType = returnType.GetGenericTypeDefinition();

            if (genericType == typeof(System.Threading.Tasks.Task<>))
            {
                IReturnHandler<TPacket> innerHandler = GetHandler(genericArg);
                return CreateTaskHandler(innerHandler, genericArg);
            }

            if (genericType == typeof(System.Threading.Tasks.ValueTask<>))
            {
                IReturnHandler<TPacket> innerHandler = GetHandler(genericArg);
                return CreateValueTaskHandler(innerHandler, genericArg);
            }
        }

        // Fallback cho unsupported types
        return new UnsupportedReturnHandler<TPacket>(returnType);
    }

    /// <summary>
    /// Create base handlers dictionary.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Collections.Frozen.FrozenDictionary<System.Type, IReturnHandler<TPacket>> CreateHandlers()
    {
        System.Collections.Generic.Dictionary<System.Type, IReturnHandler<TPacket>> handlers = new()
        {
            [typeof(void)] = new VoidReturnHandler<TPacket>(),
            [typeof(TPacket)] = new PacketReturnHandler<TPacket>(),
            [typeof(System.String)] = new StringReturnHandler<TPacket>(),
            [typeof(System.Byte[])] = new ByteArrayReturnHandler<TPacket>(),
            [typeof(System.Memory<System.Byte>)] = new MemoryReturnHandler<TPacket>(),
            [typeof(System.Threading.Tasks.Task)] = new TaskVoidReturnHandler<TPacket>(),
            [typeof(System.Threading.Tasks.ValueTask)] = new ValueTaskVoidReturnHandler<TPacket>(),
            [typeof(System.ReadOnlyMemory<System.Byte>)] = new ReadOnlyMemoryReturnHandler<TPacket>()
        };

        return System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(handlers);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static IReturnHandler<TPacket> CreateTaskHandler(
        [System.Diagnostics.CodeAnalysis.DisallowNull] IReturnHandler<TPacket> innerHandler,
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.Type resultType)
    {
        System.Type handlerType = typeof(TaskReturnHandler<,>).MakeGenericType(typeof(TPacket), resultType);
        return (IReturnHandler<TPacket>)System.Activator.CreateInstance(handlerType, innerHandler)!;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static IReturnHandler<TPacket> CreateValueTaskHandler(
        [System.Diagnostics.CodeAnalysis.DisallowNull] IReturnHandler<TPacket> innerHandler,
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.Type resultType)
    {
        System.Type handlerType = typeof(ValueTaskReturnHandler<,>).MakeGenericType(typeof(TPacket), resultType);
        return (IReturnHandler<TPacket>)System.Activator.CreateInstance(handlerType, innerHandler)!;
    }
}