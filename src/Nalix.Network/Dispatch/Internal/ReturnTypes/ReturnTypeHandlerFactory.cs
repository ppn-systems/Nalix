﻿using Nalix.Network.Dispatch.Internal.ReturnTypes.Memory;
using Nalix.Network.Dispatch.Internal.ReturnTypes.Packet;
using Nalix.Network.Dispatch.Internal.ReturnTypes.Primitives;
using Nalix.Network.Dispatch.Internal.ReturnTypes.Task;
using Nalix.Network.Dispatch.Internal.ReturnTypes.Void;

namespace Nalix.Network.Dispatch.Internal.ReturnTypes;

/// <summary>
/// A zero-allocation factory responsible for returning the appropriate 
/// IReturnHandler{TPacket} instance based on a method's return type.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type used for handling communication. Must implement
/// IPacket, IPacketFactory, IPacketEncryptor, and IPacketCompressor.
/// </typeparam>
internal static class ReturnTypeHandlerFactory<TPacket>
    where TPacket : Common.Package.IPacket,
                   Common.Package.IPacketFactory<TPacket>,
                   Common.Package.IPacketEncryptor<TPacket>,
                   Common.Package.IPacketCompressor<TPacket>
{
    /// <summary>
    /// Cached handlers để tránh recreation.
    /// </summary>
    private static readonly System.Collections.Frozen.FrozenDictionary<System.Type, IReturnHandler<TPacket>> _handlers;

    static ReturnTypeHandlerFactory()
    {
        // Ensure the factory is initialized with the correct packet type.
        if (!typeof(TPacket).IsAssignableTo(typeof(Common.Package.IPacket)))
        {
            throw new System.ArgumentException(
                $"TPacket must implement {nameof(Common.Package.IPacket)}.");
        }

        _handlers = CreateHandlers();
    }

    /// <summary>
    /// Get handler cho specific return type.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static IReturnHandler<TPacket> GetHandler(System.Type returnType)
    {
        if (_handlers.TryGetValue(returnType, out var handler))
        {
            return handler;
        }

        // Handle generic Task<T> và ValueTask<T>
        if (returnType.IsGenericType)
        {
            var genericType = returnType.GetGenericTypeDefinition();
            var genericArg = returnType.GetGenericArguments()[0];

            if (genericType == typeof(System.Threading.Tasks.Task<>))
            {
                var innerHandler = GetHandler(genericArg);
                return CreateTaskHandler(innerHandler, genericArg);
            }

            if (genericType == typeof(System.Threading.Tasks.ValueTask<>))
            {
                var innerHandler = GetHandler(genericArg);
                return CreateValueTaskHandler(innerHandler, genericArg);
            }
        }

        // Fallback cho unsupported types
        return new UnsupportedReturnHandler<TPacket>(returnType);
    }

    /// <summary>
    /// Create base handlers dictionary.
    /// </summary>
    private static System.Collections.Frozen.FrozenDictionary<System.Type, IReturnHandler<TPacket>> CreateHandlers()
    {
        var handlers = new System.Collections.Generic.Dictionary<System.Type, IReturnHandler<TPacket>>
        {
            [typeof(void)] = new VoidReturnHandler<TPacket>(),
            [typeof(TPacket)] = new PacketReturnHandler<TPacket>(),
            [typeof(System.Byte[])] = new ByteArrayReturnHandler<TPacket>(),
            [typeof(System.String)] = new StringReturnHandler<TPacket>(),
            [typeof(System.Memory<System.Byte>)] = new MemoryReturnHandler<TPacket>(),
            [typeof(System.ReadOnlyMemory<System.Byte>)] = new ReadOnlyMemoryReturnHandler<TPacket>(),
            [typeof(System.Threading.Tasks.Task)] = new TaskVoidReturnHandler<TPacket>(),
            [typeof(System.Threading.Tasks.ValueTask)] = new ValueTaskVoidReturnHandler<TPacket>(),
        };

        return System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(handlers);
    }

    private static IReturnHandler<TPacket> CreateTaskHandler(
        IReturnHandler<TPacket> innerHandler,
        System.Type resultType)
    {
        var handlerType = typeof(TaskReturnHandler<,>).MakeGenericType(typeof(TPacket), resultType);
        return (IReturnHandler<TPacket>)System.Activator.CreateInstance(handlerType, innerHandler)!;
    }

    private static IReturnHandler<TPacket> CreateValueTaskHandler(
        IReturnHandler<TPacket> innerHandler,
        System.Type resultType)
    {
        var handlerType = typeof(ValueTaskReturnHandler<,>).MakeGenericType(typeof(TPacket), resultType);
        return (IReturnHandler<TPacket>)System.Activator.CreateInstance(handlerType, innerHandler)!;
    }
}