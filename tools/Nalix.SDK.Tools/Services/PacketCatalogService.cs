// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Serialization;
using Nalix.Common.Primitives;
using Nalix.Framework.DataFrames;
using Nalix.Framework.Identifiers;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Extensions;
using Nalix.SDK.Tools.Models;

namespace Nalix.SDK.Tools.Services;

/// <summary>
/// Builds the packet catalog used by the WPF packet testing tool.
/// </summary>
public sealed class PacketCatalogService : IPacketCatalogService
{
    private readonly HashSet<string> _probeDirectories = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<Type, PacketTypeDescriptor> _descriptorByType = new Dictionary<Type, PacketTypeDescriptor>();

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketCatalogService"/> class.
    /// </summary>
    public PacketCatalogService()
    {
        _ = _probeDirectories.Add(AppContext.BaseDirectory);
        AssemblyLoadContext.Default.Resolving += this.ResolveAssembly;
        this.LoadNalixAssemblies();
        this.Catalog = this.BuildCatalog();
    }

    /// <inheritdoc/>
    public PacketCatalog Catalog { get; private set; }

    /// <inheritdoc/>
    public PacketCatalog LoadPacketAssembly(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentException("The assembly path cannot be null or whitespace.", nameof(assemblyPath));
        }

        string fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The selected packet assembly could not be found.", fullPath);
        }

        _ = _probeDirectories.Add(Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory);
        Assembly assembly = this.LoadAssembly(fullPath);
        IReadOnlyList<Type> packetTypes = this.GetPacketTypes(assembly, throwOnLoaderErrors: true);
        if (packetTypes.Count == 0)
        {
            throw new InvalidOperationException($"No IPacket implementations were found in {Path.GetFileName(fullPath)}.");
        }

        this.Catalog = this.BuildCatalog();
        return this.Catalog;
    }

    /// <inheritdoc/>
    public PacketTypeDescriptor? FindByType(Type packetType)
        => _descriptorByType.TryGetValue(packetType, out PacketTypeDescriptor? descriptor) ? descriptor : null;

    /// <inheritdoc/>
    public IPacket CreatePacket(PacketTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (Activator.CreateInstance(descriptor.PacketType) is not IPacket packet)
        {
            throw new InvalidOperationException($"Cannot create packet instance for {descriptor.PacketType.FullName}.");
        }

        return packet;
    }

    /// <inheritdoc/>
    public IPacket Deserialize(byte[] rawBytes)
    {
        if (this.Catalog.Registry.Deserialize(rawBytes) is not IPacket packet)
        {
            throw new InvalidOperationException("The packet registry returned a non-packet instance.");
        }

        return packet;
    }

    private void LoadNalixAssemblies()
    {
        string baseDirectory = AppContext.BaseDirectory;
        foreach (string path in Directory.EnumerateFiles(baseDirectory, "Nalix*.dll", SearchOption.TopDirectoryOnly))
        {
            _ = this.TryLoadAssembly(path);
        }
    }

    private Assembly LoadAssembly(string assemblyPath)
    {
        string fullPath = Path.GetFullPath(assemblyPath);
        AssemblyName expectedAssemblyName = AssemblyName.GetAssemblyName(fullPath);
        Assembly? existingAssembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, expectedAssemblyName.Name, StringComparison.OrdinalIgnoreCase));

        if (existingAssembly is not null)
        {
            return existingAssembly;
        }

        return AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
    }

    private Assembly? ResolveAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        Assembly? existingAssembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));

        if (existingAssembly is not null)
        {
            return existingAssembly;
        }

        foreach (string directory in _probeDirectories)
        {
            string? candidatePath = Path.Combine(directory, $"{assemblyName.Name}.dll");
            if (!File.Exists(candidatePath))
            {
                candidatePath = this.FindAssemblyPath(directory, assemblyName.Name);
                if (candidatePath is null)
                {
                    continue;
                }
            }

            try
            {
                return context.LoadFromAssemblyPath(candidatePath);
            }
            catch
            {
                // Continue probing other known directories.
            }
        }

        return null;
    }

    private bool TryLoadAssembly(string assemblyPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(assemblyPath);
            AssemblyName expectedAssemblyName = AssemblyName.GetAssemblyName(fullPath);
            bool alreadyLoaded = AppDomain.CurrentDomain
                .GetAssemblies()
                .Any(assembly => string.Equals(assembly.GetName().Name, expectedAssemblyName.Name, StringComparison.OrdinalIgnoreCase));

            if (alreadyLoaded)
            {
                return false;
            }

            _ = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
            return true;
        }
        catch
        {
            // Best-effort load only; skip assemblies that cannot be loaded.
            return false;
        }
    }

    private string? FindAssemblyPath(string rootDirectory, string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || string.IsNullOrWhiteSpace(assemblyName) || !Directory.Exists(rootDirectory))
        {
            return null;
        }

        try
        {
            return Directory
                .EnumerateFiles(rootDirectory, $"{assemblyName}.dll", SearchOption.AllDirectories)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private IReadOnlyList<Type> GetPacketTypes(Assembly assembly, bool throwOnLoaderErrors)
    {
        try
        {
            return [.. assembly
                .GetTypes()
                .Where(static type => type.IsClass && !type.IsAbstract && typeof(IPacket).IsAssignableFrom(type))];
        }
        catch (ReflectionTypeLoadException exception)
        {
            List<Type> packetTypes =
            [
                .. exception.Types
                    .OfType<Type>()
                    .Where(static type => type.IsClass && !type.IsAbstract && typeof(IPacket).IsAssignableFrom(type))
            ];

            if (packetTypes.Count > 0 || !throwOnLoaderErrors)
            {
                return packetTypes;
            }

            string loaderMessage = exception.LoaderExceptions
                .Where(static loaderException => loaderException is not null && !string.IsNullOrWhiteSpace(loaderException.Message))
                .Select(static loaderException => loaderException!.Message)
                .FirstOrDefault()
                ?? exception.Message;

            throw new InvalidOperationException(loaderMessage, exception);
        }
    }

    private PacketCatalog BuildCatalog()
    {
        PacketRegistryFactory factory = new();
        _ = factory.IncludeCurrentDomain();

        PacketRegistry registry = factory.CreateCatalog();

        List<PacketTypeDescriptor> descriptors =
        [
            .. AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(static assembly => !assembly.IsDynamic)
                .SelectMany(static assembly => assembly.GetLoadableTypes())
                .Where(static type => type.IsClass && !type.IsAbstract && typeof(IPacket).IsAssignableFrom(type))
                .Distinct()
                .Select(this.BuildDescriptor)
                .OrderBy(static descriptor => descriptor.Name, StringComparer.Ordinal)
        ];

        int maxNameLength = descriptors.Count == 0
            ? 0
            : descriptors.Max(static descriptor => descriptor.Name.Length);

        List<PacketTypeDescriptor> normalizedDescriptors =
        [
            .. descriptors.Select(
                descriptor => new PacketTypeDescriptor
                {
                    PacketType = descriptor.PacketType,
                    Name = descriptor.Name,
                    FullName = descriptor.FullName,
                    MagicNumber = descriptor.MagicNumber,
                    Properties = descriptor.Properties,
                    PaddedName = descriptor.Name.PadRight(maxNameLength)
                })
        ];

        PacketCatalog catalog = new()
        {
            Registry = registry,
            PacketTypes = new ReadOnlyCollection<PacketTypeDescriptor>(normalizedDescriptors)
        };

        _descriptorByType = catalog.PacketTypes.ToDictionary(static descriptor => descriptor.PacketType);
        return catalog;
    }

    private PacketTypeDescriptor BuildDescriptor(Type packetType)
    {
        return new PacketTypeDescriptor
        {
            PacketType = packetType,
            Name = packetType.Name,
            FullName = packetType.FullName ?? packetType.Name,
            MagicNumber = PacketRegistryFactory.Compute(packetType),
            Properties = this.BuildPropertyDefinitions(packetType, [])
        };
    }

    private IReadOnlyList<PacketPropertyDefinition> BuildPropertyDefinitions(Type type, HashSet<Type> path)
    {
        if (!path.Add(type))
        {
            return Array.Empty<PacketPropertyDefinition>();
        }

        try
        {
            SerializeLayout layout = type.GetCustomAttribute<SerializePackableAttribute>()?.SerializeLayout ?? SerializeLayout.Auto;
            List<PropertyInfo> properties = [.. type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property =>
                    property.GetMethod is not null &&
                    property.GetIndexParameters().Length == 0 &&
                    property.GetCustomAttribute<SerializeIgnoreAttribute>() is null)
                .Where(property => layout != SerializeLayout.Explicit ||
                    property.GetCustomAttribute<SerializeOrderAttribute>() is not null ||
                    property.GetCustomAttribute<SerializeHeaderAttribute>() is not null)
                .OrderBy(static property => property.GetCustomAttribute<SerializeHeaderAttribute>() is null ? 1 : 0)
                .ThenBy(static property => property.GetCustomAttribute<SerializeHeaderAttribute>()?.Order ?? int.MaxValue)
                .ThenBy(static property => property.GetCustomAttribute<SerializeOrderAttribute>()?.Order ?? int.MaxValue)
                .ThenBy(static property => property.MetadataToken)];

            return
            [
                .. properties.Select(property => new PacketPropertyDefinition
                {
                    Name = property.Name,
                    DisplayName = this.SplitPascalCase(property.Name),
                    PropertyType = property.PropertyType,
                    EditorKind = this.ResolveEditorKind(property.PropertyType),
                    IsHeader = property.GetCustomAttribute<SerializeHeaderAttribute>() is not null,
                    Children = this.IsComplexType(property.PropertyType)
                        ? this.BuildPropertyDefinitions(this.GetNonNullableType(property.PropertyType), path)
                        : Array.Empty<PacketPropertyDefinition>()
                })
            ];
        }
        finally
        {
            _ = path.Remove(type);
        }
    }

    private EditorKind ResolveEditorKind(Type type)
    {
        Type effectiveType = this.GetNonNullableType(type);

        if (effectiveType == typeof(string))
        {
            return EditorKind.Text;
        }

        if (effectiveType == typeof(byte[]) || effectiveType == typeof(Bytes32) || effectiveType == typeof(Snowflake))
        {
            return EditorKind.ByteArray;
        }

        if (effectiveType == typeof(bool))
        {
            return EditorKind.Boolean;
        }

        if (effectiveType.IsEnum)
        {
            return EditorKind.Enum;
        }

        if (this.IsNumericType(effectiveType))
        {
            return EditorKind.Numeric;
        }

        if (this.IsComplexType(effectiveType))
        {
            return EditorKind.Complex;
        }

        return EditorKind.Unsupported;
    }

    private bool IsComplexType(Type type)
    {
        Type effectiveType = this.GetNonNullableType(type);
        return effectiveType != typeof(string)
            && effectiveType != typeof(byte[])
            && !effectiveType.IsPrimitive
            && !effectiveType.IsEnum
            && effectiveType != typeof(decimal)
            && effectiveType != typeof(DateTime)
            && effectiveType != typeof(DateTimeOffset)
            && effectiveType != typeof(TimeSpan)
            && effectiveType != typeof(Guid)
            && effectiveType != typeof(Bytes32)
            && effectiveType != typeof(Snowflake);
    }

    private bool IsNumericType(Type type)
    {
        Type effectiveType = this.GetNonNullableType(type);
        return effectiveType == typeof(byte)
            || effectiveType == typeof(sbyte)
            || effectiveType == typeof(short)
            || effectiveType == typeof(ushort)
            || effectiveType == typeof(int)
            || effectiveType == typeof(uint)
            || effectiveType == typeof(long)
            || effectiveType == typeof(ulong)
            || effectiveType == typeof(float)
            || effectiveType == typeof(double)
            || effectiveType == typeof(decimal);
    }

    private Type GetNonNullableType(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    private string SplitPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        List<char> characters = new(value.Length + 4);
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (index > 0 && char.IsUpper(current) && !char.IsUpper(value[index - 1]))
            {
                characters.Add(' ');
            }

            characters.Add(current);
        }

        return new string([.. characters]);
    }

    /// <summary>
    /// Reads a packet snapshot from raw bytes when packet deserialization fails.
    /// </summary>
    /// <param name="rawBytes">The raw bytes.</param>
    /// <returns>The extracted snapshot.</returns>
    public static PacketSnapshot CreateSnapshotFromRaw(byte[] rawBytes)
    {
        ArgumentNullException.ThrowIfNull(rawBytes);

        uint magicNumber = rawBytes.Length >= sizeof(uint)
            ? BinaryPrimitives.ReadUInt32LittleEndian(rawBytes.AsSpan(0, sizeof(uint)))
            : 0u;

        ushort opCode = rawBytes.Length >= sizeof(uint) + sizeof(ushort)
            ? BinaryPrimitives.ReadUInt16LittleEndian(rawBytes.AsSpan(sizeof(uint), sizeof(ushort)))
            : (ushort)0;

        return new PacketSnapshot
        {
            PacketTypeName = ToolResourceHelper.GetTexts().UnknownPacketName,
            RawBytes = rawBytes,
            OpCode = opCode,
            MagicNumber = magicNumber
        };
    }
}
