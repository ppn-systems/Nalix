// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using Nalix.Abstractions.Networking.Packets;

namespace Nalix.Runtime.Dispatching;

/// <summary>
/// A registry that acts as a bridge between the compile-time source generated handler bootstraps
/// and the runtime <see cref="Routing.PacketDispatchOptions{TPacket}"/>.
/// </summary>
public static class PacketHandlerRegistry
{
    private static readonly ConcurrentDictionary<Type, IPacketHandlerCompiler> s_compilers = new();

    /// <summary>
    /// Registers a source-generated compiler callback for a specific controller type.
    /// This method is called by the generated ModuleInitializer.
    /// </summary>
    /// <param name="controllerType">The type of the controller.</param>
    /// <param name="compiler">The callback to build the handlers.</param>
    public static void Register(Type controllerType, IPacketHandlerCompiler compiler) => s_compilers[controllerType] = compiler;

    /// <summary>
    /// Attempts to build handlers for the specified controller type using the registered compiler.
    /// </summary>
    /// <param name="controllerType">The controller type to resolve.</param>
    /// <param name="builder">The handler builder to receive the definitions.</param>
    /// <param name="factory">The factory to instantiate the controller if needed.</param>
    /// <returns>True if a compiler was found and handlers were built; otherwise, false.</returns>
    internal static bool TryBuildHandlers<TPacket>(Type controllerType, IPacketHandlerBuilder<TPacket> builder, Func<object> factory) where TPacket : IPacket
    {
        if (s_compilers.TryGetValue(controllerType, out IPacketHandlerCompiler? compiler))
        {
            compiler.InitializeDependencies();
            compiler.Build(builder, factory);
            return true;
        }

        return false;
    }
}
