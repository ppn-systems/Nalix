// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.Contracts;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;

namespace Nalix.Runtime.Internal.Compilation;

internal sealed partial class PacketHandlerCompiler<TController, TPacket>
{
    /// <summary>
    /// Builds the argument expression array for the compiled method-call expression.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Expression[] BUILD_ARG_EXPRESSIONS(
        SignatureKind kind, ParameterInfo[] parms, ParameterExpression context,
        MemberExpression packetExpr, MemberExpression connectionExpr, MemberExpression ctExpr)
    {
        switch (kind)
        {
            case SignatureKind.ContextOnly:
                {
                    Type paramCtxType = parms[0].ParameterType;
                    Expression ctxArg =
                        paramCtxType == context.Type
                        ? context
                        : System.Linq.Expressions.Expression.Convert(context, paramCtxType);

                    return [ctxArg];
                }

            case SignatureKind.ContextWithToken:
                {
                    Type paramCtxType = parms[0].ParameterType;
                    Expression ctxArg =
                        paramCtxType == context.Type
                        ? context
                        : System.Linq.Expressions.Expression.Convert(context, paramCtxType);

                    return [ctxArg, ctExpr];
                }

            case SignatureKind.LegacyNoToken:
                {
                    Type packetType = parms[0].ParameterType;
                    Type connType = parms[1].ParameterType;

                    Expression pktArg = packetType.IsAssignableFrom(typeof(TPacket))
                        ? packetExpr
                        : System.Linq.Expressions.Expression.Convert(packetExpr, packetType);

                    Expression connArg = connType == typeof(IConnection)
                        ? connectionExpr
                        : System.Linq.Expressions.Expression.Convert(connectionExpr, connType);

                    return [pktArg, connArg];
                }

            case SignatureKind.LegacyWithToken:
                {
                    Type packetType = parms[0].ParameterType;
                    Type connType = parms[1].ParameterType;

                    Expression pktArg = packetType.IsAssignableFrom(typeof(TPacket))
                        ? packetExpr
                        : System.Linq.Expressions.Expression.Convert(packetExpr, packetType);

                    Expression connArg = connType == typeof(IConnection)
                        ? connectionExpr
                        : System.Linq.Expressions.Expression.Convert(connectionExpr, connType);

                    return [pktArg, connArg, ctExpr];
                }

            case SignatureKind.LegacyConcreteNoToken:
                {
                    Type packetType = parms[0].ParameterType;
                    Type connType = parms[1].ParameterType;

                    Expression pktArg = Expression.Convert(packetExpr, packetType);

                    Expression connArg = connType == typeof(IConnection)
                        ? connectionExpr
                        : System.Linq.Expressions.Expression.Convert(connectionExpr, connType);

                    return [pktArg, connArg];
                }

            case SignatureKind.LegacyConcreteWithToken:
                {
                    Type packetType = parms[0].ParameterType;
                    Type connType = parms[1].ParameterType;

                    Expression pktArg = Expression.Convert(packetExpr, packetType);

                    Expression connArg = connType == typeof(IConnection)
                        ? connectionExpr
                        : System.Linq.Expressions.Expression.Convert(connectionExpr, connType);

                    return [pktArg, connArg, ctExpr];
                }

            case SignatureKind.MemoryNoToken:
                {
                    Type connType = parms[1].ParameterType;
                    Expression rawMemArg = Expression.Property(Expression.Convert(packetExpr, typeof(MemoryPacket)), nameof(MemoryPacket.Memory));

                    Expression connArg = connType == typeof(IConnection)
                        ? connectionExpr
                        : System.Linq.Expressions.Expression.Convert(connectionExpr, connType);

                    return [rawMemArg, connArg];
                }

            case SignatureKind.MemoryWithToken:
                {
                    Type connType = parms[1].ParameterType;
                    Expression rawMemArg = Expression.Property(Expression.Convert(packetExpr, typeof(MemoryPacket)), nameof(MemoryPacket.Memory));

                    Expression connArg = connType == typeof(IConnection)
                        ? connectionExpr
                        : System.Linq.Expressions.Expression.Convert(connectionExpr, connType);

                    return [rawMemArg, connArg, ctExpr];
                }

            default:
                THROW_KIND_OUT_OF_RANGE(kind);
                return null;
        }
    }
}
