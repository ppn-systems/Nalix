// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Internal.Results;

namespace Nalix.Runtime.Internal.Compilation;

/// <summary>
/// High-performance controller scanner with caching and zero-allocation lookups.
/// Uses compiled expression trees for maximum dispatch performance.
/// </summary>
/// <typeparam name="TController">The controller type to scan.</typeparam>
/// <typeparam name="TPacket">The packet type handled by this controller.</typeparam>
[DebuggerNonUserCode]
[SkipLocalsInit]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed partial class PacketHandlerCompiler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TController, TPacket>()
    where TController : class where TPacket : IPacket
{
    #region Fields

    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    /// <summary>
    /// Caches attribute lookups per method for performance.
    /// </summary>
    private static readonly ConcurrentDictionary<MethodInfo, PacketMetadata> s_attributeCache = new();

    /// <summary>
    /// Caches compiled method delegates for each controller type to eliminate reflection.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, FrozenDictionary<ushort, PacketHandlerDescriptor<TPacket>>> s_compiledMethodCache = new();

    #endregion Fields

    #region APIs

    /// <summary>
    /// Scans the controller and returns an array of packet handler delegates.
    /// </summary>
    /// <param name="factory">A factory method that creates a controller instance.</param>
    /// <returns>An array of compiled packet handler delegates.</returns>
    /// <exception cref="InternalErrorException"></exception>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    public static PacketHandler<TPacket>[] CompileHandlers(Func<TController> factory)
    {
        Type controllerType = typeof(TController);

        // Ensure controller has [PacketController] attribute
        PacketControllerAttribute controllerAttr = CustomAttributeExtensions.GetCustomAttribute<PacketControllerAttribute>(controllerType)
            ?? throw new InternalErrorException($"Controller '{controllerType.Name}' is missing the [PacketController] attribute.");

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            DiagnosticsEvents.Source.Write(
                DiagnosticsEvents.Internal.Debug,
                new DiagnosticLog(
                    "RT.PacketHandlerCompiler:CompileHandlers",
                    $"scan controller={controllerType.Name}"));
        }

        // Reuse cached method metadata when possible; otherwise compile once and
        // freeze the result so dispatch stays allocation-free at runtime.
        FrozenDictionary<ushort, PacketHandlerDescriptor<TPacket>> compiledMethods = COMPILE_CONTROLLER_HANDLERS(controllerType);

        // Create one controller instance up front and reuse it for every handler.
        TController controllerInstance = factory();

        // CreateCatalog delegate descriptors
        PacketHandler<TPacket>[] descriptors = new PacketHandler<TPacket>[compiledMethods.Count];
        int index = 0;

        foreach ((ushort opCode, PacketHandlerDescriptor<TPacket> compiledMethod) in compiledMethods)
        {
            PacketMetadata attributes = GET_PACKET_METADATA(compiledMethod.MethodInfo);

            descriptors[index++] = new PacketHandler<TPacket>(
                opCode,
                attributes,
                controllerInstance,
                compiledMethod.MethodInfo,
                compiledMethod.ReturnType,
                compiledMethod.CompiledInvoker,
                expectedPacketType: compiledMethod.ExpectedPacketType,
                returnHandler: ReturnTypeHandlerFactory<TPacket>.ResolveHandler(compiledMethod.ReturnType));
        }

        string firstOps = string.Join(",", Enumerable
                                              .Select(Enumerable
                                              .Take(compiledMethods.Keys, 6), o => $"0x{o:X4}"));

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            string opsSuffix = compiledMethods.Count > 6 ? ",..." : string.Empty;
            DiagnosticsEvents.Source.Write(
                DiagnosticsEvents.Internal.Debug,
                new DiagnosticLog(
                    "RT.PacketHandlerCompiler:CompileHandlers",
                    $"found count={compiledMethods.Count} controller={controllerType.FullName} ops=[{firstOps}{opsSuffix}]"));
        }

        return descriptors;
    }

    #endregion APIs
}
