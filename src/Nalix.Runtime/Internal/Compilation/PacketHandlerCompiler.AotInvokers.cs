// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;

using Nalix.Runtime.Dispatching;
namespace Nalix.Runtime.Internal.Compilation;

internal sealed partial class PacketHandlerCompiler<TController, TPacket>
{
    [Pure]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Func<object, PacketContext<TPacket>, object> BUILD_AOT_INVOKER(MethodInfo method, ParameterInfo[] parms, SignatureKind kind)
    {
        bool isVoid = method.ReturnType == typeof(void);

        return kind switch
        {
            SignatureKind.ContextOnly => BUILD_CONTEXT_ONLY_INVOKER(method, isVoid),
            SignatureKind.ContextWithToken => BUILD_CONTEXT_WITH_TOKEN_INVOKER(method, isVoid),
            SignatureKind.LegacyNoToken => BUILD_LEGACY_INVOKER(method, parms, withToken: false),
            SignatureKind.LegacyWithToken => BUILD_LEGACY_INVOKER(method, parms, withToken: true),
            SignatureKind.LegacyConcreteNoToken => BUILD_LEGACY_INVOKER(method, parms, withToken: false),
            SignatureKind.LegacyConcreteWithToken => BUILD_LEGACY_INVOKER(method, parms, withToken: true),
            SignatureKind.MemoryNoToken => BUILD_RAW_MEMORY_INVOKER(method, isVoid, withToken: false),
            SignatureKind.MemoryWithToken => BUILD_RAW_MEMORY_INVOKER(method, isVoid, withToken: true),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Func<object, PacketContext<TPacket>, object> BUILD_RAW_MEMORY_INVOKER(MethodInfo method, bool isVoid, bool withToken)
    {
        if (method.IsStatic)
        {
            if (!withToken)
            {
                if (isVoid)
                {
                    Action<ReadOnlyMemory<byte>, IConnection> invoker = method.CreateDelegate<Action<ReadOnlyMemory<byte>, IConnection>>();
                    return (_, context) => { invoker(((MemoryPacket)(object)context.Packet!).Memory, context.Connection); return null!; };
                }
                else
                {
                    Func<ReadOnlyMemory<byte>, IConnection, object> invoker = method.CreateDelegate<Func<ReadOnlyMemory<byte>, IConnection, object>>();
                    return (_, context) => invoker(((MemoryPacket)(object)context.Packet!).Memory, context.Connection);
                }
            }
            else
            {
                if (isVoid)
                {
                    Action<ReadOnlyMemory<byte>, IConnection, CancellationToken> invoker = method.CreateDelegate<Action<ReadOnlyMemory<byte>, IConnection, CancellationToken>>();
                    return (_, context) => { invoker(((MemoryPacket)(object)context.Packet!).Memory, context.Connection, context.CancellationToken); return null!; };
                }
                else
                {
                    Func<ReadOnlyMemory<byte>, IConnection, CancellationToken, object> invoker = method.CreateDelegate<Func<ReadOnlyMemory<byte>, IConnection, CancellationToken, object>>();
                    return (_, context) => invoker(((MemoryPacket)(object)context.Packet!).Memory, context.Connection, context.CancellationToken);
                }
            }
        }
        else
        {
            if (!withToken)
            {
                if (isVoid)
                {
                    Action<TController, ReadOnlyMemory<byte>, IConnection> invoker = method.CreateDelegate<Action<TController, ReadOnlyMemory<byte>, IConnection>>();
                    return (instance, context) => { invoker((TController)instance, ((MemoryPacket)(object)context.Packet!).Memory, context.Connection); return null!; };
                }
                else
                {
                    Func<TController, ReadOnlyMemory<byte>, IConnection, object> invoker = method.CreateDelegate<Func<TController, ReadOnlyMemory<byte>, IConnection, object>>();
                    return (instance, context) => invoker((TController)instance, ((MemoryPacket)(object)context.Packet!).Memory, context.Connection);
                }
            }
            else
            {
                if (isVoid)
                {
                    Action<TController, ReadOnlyMemory<byte>, IConnection, CancellationToken> invoker = method.CreateDelegate<Action<TController, ReadOnlyMemory<byte>, IConnection, CancellationToken>>();
                    return (instance, context) => { invoker((TController)instance, ((MemoryPacket)(object)context.Packet!).Memory, context.Connection, context.CancellationToken); return null!; };
                }
                else
                {
                    Func<TController, ReadOnlyMemory<byte>, IConnection, CancellationToken, object> invoker = method.CreateDelegate<Func<TController, ReadOnlyMemory<byte>, IConnection, CancellationToken, object>>();
                    return (instance, context) => invoker((TController)instance, ((MemoryPacket)(object)context.Packet!).Memory, context.Connection, context.CancellationToken);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Func<object, PacketContext<TPacket>, object> BUILD_CONTEXT_ONLY_INVOKER(MethodInfo method, bool isVoid)
    {
        if (method.IsStatic)
        {
            if (isVoid)
            {
                Action<PacketContext<TPacket>> invoker = method.CreateDelegate<Action<PacketContext<TPacket>>>();
                return (_, context) => { invoker(context); return null!; };
            }
            else
            {
                Func<PacketContext<TPacket>, object> invoker = method.CreateDelegate<Func<PacketContext<TPacket>, object>>();
                return (_, context) => invoker(context);
            }
        }
        else
        {
            if (isVoid)
            {
                Action<TController, PacketContext<TPacket>> invoker = method.CreateDelegate<Action<TController, PacketContext<TPacket>>>();
                return (instance, context) => { invoker((TController)instance, context); return null!; };
            }
            else
            {
                Func<TController, PacketContext<TPacket>, object> invoker = method.CreateDelegate<Func<TController, PacketContext<TPacket>, object>>();
                return (instance, context) => invoker((TController)instance, context);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Func<object, PacketContext<TPacket>, object> BUILD_CONTEXT_WITH_TOKEN_INVOKER(MethodInfo method, bool isVoid)
    {
        if (method.IsStatic)
        {
            if (isVoid)
            {
                Action<PacketContext<TPacket>, CancellationToken> invoker = method.CreateDelegate<Action<PacketContext<TPacket>, CancellationToken>>();
                return (_, context) => { invoker(context, context.CancellationToken); return null!; };
            }
            else
            {
                Func<PacketContext<TPacket>, CancellationToken, object> invoker = method.CreateDelegate<Func<PacketContext<TPacket>, CancellationToken, object>>();
                return (_, context) => invoker(context, context.CancellationToken);
            }
        }
        else
        {
            if (isVoid)
            {
                Action<TController, PacketContext<TPacket>, CancellationToken> invoker = method.CreateDelegate<Action<TController, PacketContext<TPacket>, CancellationToken>>();
                return (instance, context) => { invoker((TController)instance, context, context.CancellationToken); return null!; };
            }
            else
            {
                Func<TController, PacketContext<TPacket>, CancellationToken, object> invoker = method.CreateDelegate<Func<TController, PacketContext<TPacket>, CancellationToken, object>>();
                return (instance, context) => invoker((TController)instance, context, context.CancellationToken);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Func<object, PacketContext<TPacket>, object> BUILD_LEGACY_INVOKER(MethodInfo method, ParameterInfo[] parms, bool withToken)
    {
        Type packetType = parms[0].ParameterType;
        bool typesMatch = packetType == typeof(TPacket);

        if (typesMatch)
        {
            return BUILD_LEGACY_TYPED_INVOKER(method);
        }

        return BUILD_LEGACY_FALLBACK_INVOKER(method, withToken);
    }

    private static Func<object, PacketContext<TPacket>, object> BUILD_LEGACY_TYPED_INVOKER(MethodInfo method)
    {
        bool isVoid = method.ReturnType == typeof(void);
        bool isStatic = method.IsStatic;
        bool withToken = method.GetParameters().Length == 3;

        if (!withToken)
        {
            if (isStatic)
            {
                if (isVoid)
                {
                    Action<TPacket, IConnection> invoker = method.CreateDelegate<Action<TPacket, IConnection>>();
                    return (_, context) => { invoker(context.Packet, context.Connection); return null!; };
                }
                else
                {
                    Func<TPacket, IConnection, object> invoker = method.CreateDelegate<Func<TPacket, IConnection, object>>();
                    return (_, context) => invoker(context.Packet, context.Connection);
                }
            }
            else
            {
                if (isVoid)
                {
                    Action<TController, TPacket, IConnection> invoker = method.CreateDelegate<Action<TController, TPacket, IConnection>>();
                    return (instance, context) => { invoker((TController)instance, context.Packet, context.Connection); return null!; };
                }
                else
                {
                    Func<TController, TPacket, IConnection, object> invoker = method.CreateDelegate<Func<TController, TPacket, IConnection, object>>();
                    return (instance, context) => invoker((TController)instance, context.Packet, context.Connection);
                }
            }
        }
        else
        {
            if (isStatic)
            {
                if (isVoid)
                {
                    Action<TPacket, IConnection, CancellationToken> invoker = method.CreateDelegate<Action<TPacket, IConnection, CancellationToken>>();
                    return (_, context) => { invoker(context.Packet, context.Connection, context.CancellationToken); return null!; };
                }
                else
                {
                    Func<TPacket, IConnection, CancellationToken, object> invoker = method.CreateDelegate<Func<TPacket, IConnection, CancellationToken, object>>();
                    return (_, context) => invoker(context.Packet, context.Connection, context.CancellationToken);
                }
            }
            else
            {
                if (isVoid)
                {
                    Action<TController, TPacket, IConnection, CancellationToken> invoker = method.CreateDelegate<Action<TController, TPacket, IConnection, CancellationToken>>();
                    return (instance, context) => { invoker((TController)instance, context.Packet, context.Connection, context.CancellationToken); return null!; };
                }
                else
                {
                    Func<TController, TPacket, IConnection, CancellationToken, object> invoker = method.CreateDelegate<Func<TController, TPacket, IConnection, CancellationToken, object>>();
                    return (instance, context) => invoker((TController)instance, context.Packet, context.Connection, context.CancellationToken);
                }
            }
        }
    }

    private static Func<object, PacketContext<TPacket>, object> BUILD_LEGACY_FALLBACK_INVOKER(MethodInfo method, bool withToken)
    {
        object[] args = withToken ? new object[4] : new object[3];

        if (!withToken)
        {
            return (instance, context) =>
            {
                args[0] = instance;
                args[1] = context.Packet;
                args[2] = context.Connection;
                return method.Invoke(instance, args)!;
            };
        }
        else
        {
            return (instance, context) =>
            {
                args[0] = instance;
                args[1] = context.Packet;
                args[2] = context.Connection;
                args[3] = context.CancellationToken;
                return method.Invoke(instance, args)!;
            };
        }
    }
}
