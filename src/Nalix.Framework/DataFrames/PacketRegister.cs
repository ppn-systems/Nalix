// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Reflection;
using Nalix.Common.Networking.Packets;

namespace Nalix.Framework.DataFrames;

/// <summary>
/// Provides convenience helpers to create <see cref="PacketRegistry"/> instances.
/// </summary>
public static class PacketRegister
{
    /// <summary>
    /// Creates a packet registry by scanning all currently loaded assemblies.
    /// </summary>
    /// <param name="requirePacketAttribute">
    /// When <see langword="true"/>, only packet types decorated with
    /// <see cref="PacketAttribute"/> are registered.
    /// </param>
    public static PacketRegistry CreateCatalogFromCurrentDomain(bool requirePacketAttribute = false)
    {
        PacketRegistryFactory factory = new();
        _ = factory.RegisterCurrentDomainPackets(requirePacketAttribute);
        return factory.CreateCatalog();
    }

    /// <summary>
    /// Creates a packet registry from a packet assembly file path.
    /// </summary>
    /// <param name="assemblyPath">Absolute or relative path to a packet assembly.</param>
    /// <param name="requirePacketAttribute">
    /// When <see langword="true"/>, only packet types decorated with
    /// <see cref="PacketAttribute"/> are registered.
    /// </param>
    public static PacketRegistry CreateCatalogFromAssemblyPath(string assemblyPath, bool requirePacketAttribute = false)
    {
        PacketRegistryFactory factory = new();
        _ = factory.RegisterPacketAssembly(assemblyPath, requirePacketAttribute);
        return factory.CreateCatalog();
    }

    /// <summary>
    /// Creates a packet registry by registering packet types from specific assemblies.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan.</param>
    /// <param name="requirePacketAttribute">
    /// When <see langword="true"/>, only packet types decorated with
    /// <see cref="PacketAttribute"/> are registered.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="assemblies"/> contains a <see langword="null"/> entry.
    /// </exception>
    public static PacketRegistry CreateCatalogFromAssemblies(IEnumerable<Assembly> assemblies, bool requirePacketAttribute = false)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        PacketRegistryFactory factory = new();
        foreach (Assembly? assembly in assemblies)
        {
            if (assembly is null)
            {
                throw new ArgumentException("Assembly collection cannot contain null values.", nameof(assemblies));
            }

            _ = factory.RegisterAllPackets(assembly, requirePacketAttribute);
        }

        return factory.CreateCatalog();
    }

    /// <summary>
    /// Creates a packet registry by registering packet types from assembly paths.
    /// </summary>
    /// <param name="assemblyPaths">Assembly file paths to load and scan.</param>
    /// <param name="requirePacketAttribute">
    /// When <see langword="true"/>, only packet types decorated with
    /// <see cref="PacketAttribute"/> are registered.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="assemblyPaths"/> contains a null/empty/whitespace entry.
    /// </exception>
    public static PacketRegistry CreateCatalogFromAssemblyPaths(IEnumerable<string> assemblyPaths, bool requirePacketAttribute = false)
    {
        ArgumentNullException.ThrowIfNull(assemblyPaths);

        PacketRegistryFactory factory = new();
        foreach (string? assemblyPath in assemblyPaths)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                throw new ArgumentException("Assembly path collection cannot contain null or whitespace values.", nameof(assemblyPaths));
            }

            _ = factory.RegisterPacketAssembly(assemblyPath, requirePacketAttribute);
        }

        return factory.CreateCatalog();
    }
}
