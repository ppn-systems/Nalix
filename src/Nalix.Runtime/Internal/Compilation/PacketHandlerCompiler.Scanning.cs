// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Runtime.Dispatching;
namespace Nalix.Runtime.Internal.Compilation;

internal sealed partial class PacketHandlerCompiler<TController, TPacket>
{
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static FrozenDictionary<ushort, PacketHandlerDescriptor<TPacket>> COMPILE_CONTROLLER_HANDLERS(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type x03)
    {
        // Get methods with [PacketOpcode] attribute
        MethodInfo[] methodInfos = Enumerable.ToArray(
            Enumerable.Where(
                x03.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.Static
                ),
                m => CustomAttributeExtensions.GetCustomAttribute<PacketOpcodeAttribute>(m) is not null));

        if (methodInfos.Length == 0)
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
            {
                DiagnosticsEvents.Source.Write(
                    DiagnosticsEvents.Internal.Debug,
                    new DiagnosticLog(
                        "RT.PacketHandlerCompiler:Internal",
                        $"no-method controller={x03.Name}"));
            }
        }

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            DiagnosticsEvents.Source.Write(
                DiagnosticsEvents.Internal.Debug,
                new DiagnosticLog(
                    "RT.PacketHandlerCompiler:Internal",
                    $"compile count={methodInfos.Length} controller={x03.Name}"));
        }

        return s_compiledMethodCache.GetOrAdd(x03, static (_, methods) =>
        {
            Dictionary<ushort, PacketHandlerDescriptor<TPacket>> compiled = new(methods.Length);

            foreach (MethodInfo method in methods)
            {
                PacketOpcodeAttribute? opcodeAttr = CustomAttributeExtensions
                    .GetCustomAttribute<PacketOpcodeAttribute>(method);

                if (opcodeAttr is null)
                {
                    continue;
                }

                if (compiled.ContainsKey(opcodeAttr.OpCode))
                {
                    string x01 = FORMAT_HANDLER_INFO(method.DeclaringType?.Name ?? "None", opcodeAttr.OpCode, method, method.ReturnType);

                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                    {
                        DiagnosticsEvents.Source.Write(
                            DiagnosticsEvents.Internal.Warning,
                            new DiagnosticLog(
                                "RT.PacketHandlerCompiler:Internal",
                                $"dup-opcode {x01}"));
                    }

                    continue;
                }

                try
                {
                    compiled[opcodeAttr.OpCode] = COMPILE_HANDLER_METHOD(method);

                    string x01 = FORMAT_HANDLER_INFO(method.DeclaringType?.Name ?? "None", opcodeAttr.OpCode, method, method.ReturnType);

                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
                    {
                        DiagnosticsEvents.Source.Write(
                            DiagnosticsEvents.Internal.Trace,
                            new DiagnosticLog(
                                "RT.PacketHandlerCompiler:Internal",
                                $"compiled {x01}"));
                    }
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    string x01 = FORMAT_HANDLER_INFO(method.DeclaringType?.Name ?? "None", opcodeAttr.OpCode, method, method.ReturnType);

                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                    {
                        DiagnosticsEvents.Source.Write(
                            DiagnosticsEvents.Internal.Error,
                            new DiagnosticLog(
                                "RT.PacketHandlerCompiler:Internal",
                                $"failed-compile {x01}",
                                ex));
                    }

                    throw; // BUG-78: Fail-fast instead of continuing with incomplete handlers
                }
            }

            return FrozenDictionary.ToFrozenDictionary(compiled);
        }, methodInfos);
    }

    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static PacketHandlerDescriptor<TPacket> COMPILE_HANDLER_METHOD(MethodInfo x22)
    {
        // Shared expression nodes — always built regardless of signature kind.
        // x00 = boxed controller instance
        // x01 = PacketContext<TPacket> (the single source-of-truth arg the
        //       compiled invoker always receives from ExecuteHandlerAsync)
        // x02..x04 = property reads off x01
        ParameterExpression x00 =
            Expression.Parameter(typeof(object), "instance");

        ParameterExpression x01 =
            Expression.Parameter(typeof(PacketContext<TPacket>), "context");

        Type contextType = typeof(PacketContext<TPacket>);
        PropertyInfo packetProperty = GET_REQUIRED_PROPERTY(contextType, nameof(PacketContext<>.Packet));
        PropertyInfo connectionProperty = GET_REQUIRED_PROPERTY(contextType, nameof(PacketContext<>.Connection));
        PropertyInfo cancellationTokenProperty = GET_REQUIRED_PROPERTY(contextType, nameof(PacketContext<>.CancellationToken));

        MemberExpression x02 =
            Expression.Property(x01, packetProperty);

        MemberExpression x03 =
            Expression.Property(x01, connectionProperty);

        MemberExpression x04 =
            Expression.Property(x01, cancellationTokenProperty);

        // Detect which of the 4 supported signatures this method uses.
        // Supported forms:
        //   Legacy  (a) (TPacket, IConnection)
        //   Legacy  (b) (TPacket, IConnection, CancellationToken)
        //   New     (c) (PacketContext<TPacket>)
        //   New     (d) (PacketContext<TPacket>, CancellationToken)
        ParameterInfo[] parms = x22.GetParameters();

        SignatureKind kind = RESOLVE_SIGNATURE_KIND(x22, parms);

        // Context-style with a DIFFERENT concrete PacketContext<T>
        //
        // When TPacket = IPacket but the handler declares PacketContext<Handshake>,
        // Expression.Convert cannot bridge PacketContext<IPacket> to PacketContext<Handshake>
        // because generic classes are invariant — no coercion operator exists between them.
        //
        // Solution: skip the expression-tree path for this case and use a reflection-based
        // bridge instead. MethodInfo.Invoke boxes arguments to object and performs the
        // assignability check at runtime via CLR rules, accepting PacketContext<Handshake>
        // without any explicit cast.
        Type? expectedPacketType = kind switch
        {
            SignatureKind.MemoryNoToken or SignatureKind.MemoryWithToken => typeof(MemoryPacket),
            SignatureKind.LegacyConcreteNoToken or SignatureKind.LegacyConcreteWithToken => parms[0].ParameterType,
            SignatureKind.ContextOnly or SignatureKind.ContextWithToken when parms[0].ParameterType.IsGenericType => parms[0].ParameterType.GetGenericArguments()[0] == typeof(TPacket) ? null : parms[0].ParameterType.GetGenericArguments()[0],
            SignatureKind.LegacyNoToken or SignatureKind.LegacyWithToken or _ => null
        };

        bool needsContextBridge =
            (kind is SignatureKind.ContextOnly or SignatureKind.ContextWithToken)
            && parms[0].ParameterType != typeof(PacketContext<TPacket>)
            && parms[0].ParameterType != typeof(IPacketContext<TPacket>);

        Func<object, PacketContext<TPacket>, ValueTask<object>> x20;

        if (needsContextBridge)
        {
            x20 = BUILD_CONTEXT_BRIDGE_INVOKER(x22, parms, kind);
        }
        else
        {
            Func<object, PacketContext<TPacket>, object> x12;

            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                // Normal expression-tree path — types match exactly.
                Expression[] x09 = BUILD_ARG_EXPRESSIONS(kind, parms, x01, x02, x03, x04);

                Expression x10 = x22.IsStatic
                    ? Expression.Call(x22, x09)
                    : Expression.Call(
                        Expression.Convert(x00, x22.DeclaringType
                            ?? throw new InternalErrorException($"Handler method '{x22.Name}' is missing a declaring type.")), x22, x09);

                Expression x11 = x22.ReturnType == typeof(void)
                    ? System.Linq.Expressions.Expression.Block(x10, System.Linq.Expressions.Expression.Constant(null, typeof(object)))
                    : System.Linq.Expressions.Expression.Convert(x10, typeof(object));

                x12 = Expression
                        .Lambda<Func<object, PacketContext<TPacket>, object>>(x11, x00, x01)
                        .Compile();
            }
            else
            {
                // AOT fallback — build invoke args at call-time from context fields.
                x12 = BUILD_AOT_INVOKER(x22, parms, kind);
            }

            x20 = WRAP_RETURN_TYPE(x12, x22.ReturnType);
        }

        return new PacketHandlerDescriptor<TPacket>(x22, x22.ReturnType, expectedPacketType, x20);
    }
}
