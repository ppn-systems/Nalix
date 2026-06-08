// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Nalix.Abstractions;
using Nalix.Framework.Injection.DI;

namespace Nalix.Framework;

/// <summary>
/// A thread-safe, multi-purpose registry that maps enum keys and component types to reportable diagnostic instances.
/// </summary>
public sealed class ReportRegistry : SingletonBase<ReportRegistry>, IReportable
{
    private readonly ConcurrentDictionary<(Enum Key, Type Type), IReportable> _registry = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportRegistry"/> class.
    /// </summary>
    /// <remarks>
    /// Internal visibility is required for the source-generated singleton activator.
    /// Still not publicly constructible — the class is <see langword="sealed"/>.
    /// </remarks>
    internal ReportRegistry()
    {
    }

    /// <summary>
    /// Registers a reportable instance for the specified enum key and type.
    /// </summary>
    /// <typeparam name="T">The diagnostic or component type under which to register.</typeparam>
    /// <param name="key">The enum key.</param>
    /// <param name="instance">The reportable instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="instance"/> is null.</exception>
    public void Register<T>(Enum key, T instance) where T : class, IReportable
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(instance);

        _registry[(key, typeof(T))] = instance;

        Type concreteType = instance.GetType();
        _registry[(key, concreteType)] = instance;

        foreach (Type iface in concreteType.GetInterfaces())
        {
            if (typeof(IReportable).IsAssignableFrom(iface))
            {
                _registry[(key, iface)] = instance;
            }
        }
    }

    /// <summary>
    /// Retrieves a registered reportable instance by its enum key and type.
    /// </summary>
    /// <typeparam name="T">The registered diagnostic or component type.</typeparam>
    /// <param name="key">The enum key used during registration.</param>
    /// <returns>The registered instance, or null if not found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public T? Get<T>(Enum key) where T : class, IReportable
    {
        ArgumentNullException.ThrowIfNull(key);

        return _registry.TryGetValue((key, typeof(T)), out IReportable? val) ? (T)val : null;
    }

    /// <summary>
    /// Unregisters the instance associated with the specified enum key and type.
    /// </summary>
    /// <typeparam name="T">The diagnostic or component type.</typeparam>
    /// <param name="key">The enum key.</param>
    /// <returns>True if the instance was found and removed; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public bool Unregister<T>(Enum key) where T : class, IReportable
    {
        ArgumentNullException.ThrowIfNull(key);

        bool removed = _registry.TryRemove((key, typeof(T)), out IReportable? instance);
        if (instance is not null)
        {
            Type concreteType = instance.GetType();
            _ = _registry.TryRemove((key, concreteType), out _);

            foreach (Type iface in concreteType.GetInterfaces())
            {
                if (typeof(IReportable).IsAssignableFrom(iface))
                {
                    _ = _registry.TryRemove((key, iface), out _);
                }
            }
        }

        return removed;
    }

    /// <summary>
    /// Clears all registrations in the registry.
    /// </summary>
    public void Clear() => _registry.Clear();

    /// <inheritdoc />
    protected override void DisposeManaged() => this.Clear();

    /// <inheritdoc />
    public void WriteReportData(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteStartArray("Registrations");

        foreach (KeyValuePair<(Enum Key, Type Type), IReportable> pair in _registry)
        {
            writer.WriteStartObject();

            Type keyType = pair.Key.Key.GetType();
            writer.WriteString("KeyType", keyType.FullName ?? keyType.Name);
            writer.WriteString("KeyValue", pair.Key.Key.ToString());
            writer.WriteString("TargetType", pair.Key.Type.FullName ?? pair.Key.Type.Name);

            writer.WritePropertyName("Data");
            pair.Value.WriteReportData(writer);

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes report data for all registered instances of type <typeparamref name="T"/> to the specified JSON writer.
    /// </summary>
    /// <typeparam name="T">The type of reportable instances to include.</typeparam>
    /// <param name="writer">The JSON writer to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is null.</exception>
    public void WriteReportData<T>(Utf8JsonWriter writer) where T : class, IReportable
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteStartArray("Registrations");

        foreach (KeyValuePair<(Enum Key, Type Type), IReportable> pair in _registry)
        {
            if (pair.Key.Type == typeof(T))
            {
                writer.WriteStartObject();

                Type keyType = pair.Key.Key.GetType();
                writer.WriteString("KeyType", keyType.FullName ?? keyType.Name);
                writer.WriteString("KeyValue", pair.Key.Key.ToString());
                writer.WriteString("TargetType", pair.Key.Type.FullName ?? pair.Key.Type.Name);

                writer.WritePropertyName("Data");
                pair.Value.WriteReportData(writer);

                writer.WriteEndObject();
            }
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <inheritdoc />
    public string GenerateReport()
    {
        StringBuilder sb = new();
        _ = sb.AppendLine("Report Registry Status:");
        _ = sb.AppendLine("-----------------------");
        foreach (KeyValuePair<(Enum Key, Type Type), IReportable> pair in _registry)
        {
            _ = sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"- Key: [{pair.Key.Key.GetType().Name}.{pair.Key.Key}] Type: {pair.Key.Type.Name} -> Instance: {pair.Value.GetType().Name}");
        }
        return sb.ToString();
    }
}
