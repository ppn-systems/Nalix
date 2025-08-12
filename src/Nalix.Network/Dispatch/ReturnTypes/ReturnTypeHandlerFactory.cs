using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.ReturnTypes.Memory;
using Nalix.Network.Dispatch.ReturnTypes.Packet;
using Nalix.Network.Dispatch.ReturnTypes.Primitives;
using Nalix.Network.Dispatch.ReturnTypes.Task;
using Nalix.Network.Dispatch.ReturnTypes.Void;

namespace Nalix.Network.Dispatch.ReturnTypes;

/// <summary>
/// A zero-allocation factory responsible for returning the appropriate 
/// IReturnHandler{TPacket} instance based on a method's return type.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type used for handling communication. Must implement IPacket.
/// </typeparam>
internal static class ReturnTypeHandlerFactory<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Cached handlers để tránh recreation.
    /// </summary>
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static IReturnHandler<TPacket> GetHandler(System.Type returnType)
    {
        if (_handlers.TryGetValue(returnType, out IReturnHandler<TPacket>? handler))
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static IReturnHandler<TPacket> CreateTaskHandler(
        IReturnHandler<TPacket> innerHandler,
        System.Type resultType)
    {
        System.Type handlerType = typeof(TaskReturnHandler<,>).MakeGenericType(typeof(TPacket), resultType);
        return (IReturnHandler<TPacket>)System.Activator.CreateInstance(handlerType, innerHandler)!;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static IReturnHandler<TPacket> CreateValueTaskHandler(
        IReturnHandler<TPacket> innerHandler,
        System.Type resultType)
    {
        System.Type handlerType = typeof(ValueTaskReturnHandler<,>).MakeGenericType(typeof(TPacket), resultType);
        return (IReturnHandler<TPacket>)System.Activator.CreateInstance(handlerType, innerHandler)!;
    }
}