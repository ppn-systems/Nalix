// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Common.Serialization;

namespace Nalix.Shared.Frames.Internal;

/// <summary>
/// Immutable cache of reflection metadata for a single serializable property on a packet type.
/// <para>
/// Eliminates repeated reflection calls in hot paths such as
/// <c>ResetForPool</c>, <c>Length</c>, and dynamic-size calculation.
/// All expensive work (attribute scanning, size computation, delegate compilation)
/// is performed exactly once at class-load time.
/// </para>
/// </summary>
internal sealed class PropertyMetadata
{
    #region Public Properties

    /// <summary>
    /// The underlying <see cref="PropertyInfo"/>.
    /// Retained for interop with APIs that require it (e.g. LiteSerializer).
    /// Prefer <see cref="GetValue"/> / <see cref="SetValue"/> in hot paths.
    /// </summary>
    public PropertyInfo Property { get; }

    /// <summary>
    /// Pre-computed fixed byte-size of this property on the wire.
    /// Zero when <see cref="IsDynamic"/> is <see langword="true"/>.
    /// </summary>
    public ushort FixedSize { get; }

    /// <summary>
    /// <see langword="true"/> when the property is annotated with
    /// <see cref="SerializeDynamicSizeAttribute"/>, meaning its wire-size
    /// must be evaluated at runtime.
    /// </summary>
    public bool IsDynamic { get; }

    /// <summary>
    /// <see langword="true"/> when the property type is <see cref="string"/>.
    /// </summary>
    public bool IsString { get; }

    /// <summary>
    /// <see langword="true"/> when the property type is <see cref="byte"/>[].
    /// </summary>
    public bool IsByteArray { get; }

    /// <summary>
    /// Cached result of <see cref="PropertyInfo.CanWrite"/>.
    /// </summary>
    public bool IsWritable { get; }

    /// <summary>
    /// <see langword="true"/> when this property has a public getter.
    /// </summary>
    public bool IsReadable { get; }

    /// <summary>
    /// Pre-computed default value used by <c>ResetForPool</c>.
    /// <list type="bullet">
    ///   <item><see cref="byte"/>[] → <see cref="Array.Empty{T}"/></item>
    ///   <item><see cref="string"/> → <see cref="string.Empty"/></item>
    ///   <item>Value type → <see cref="Activator.CreateInstance(Type)"/></item>
    ///   <item>Reference type → <see langword="null"/></item>
    /// </list>
    /// </summary>
    public object? DefaultValue { get; }

    #endregion Public Properties

    #region Private Fields

    /// <summary>
    /// True compiled open-instance delegates via Expression Trees.
    /// No MethodInfo.Invoke — no argument array allocation, no boxing round-trip.
    /// </summary>
    private readonly Func<object, object?>? _getter;

    private readonly Action<object, object?>? _setter;

    #endregion Private Fields

    #region Constructor

    /// <summary>
    /// Initializes a new <see cref="PropertyMetadata"/> by reflecting on
    /// <paramref name="prop"/> and caching all derived information.
    /// </summary>
    /// <param name="prop">A public instance property decorated with
    /// <see cref="SerializeOrderAttribute"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="prop"/> is <see langword="null"/>.
    /// </exception>
    public PropertyMetadata(PropertyInfo prop)
    {
        ArgumentNullException.ThrowIfNull(prop);

        // Guard: DeclaringType must be known to build open-instance delegates.
        if (prop.DeclaringType is null)
        {
            throw new ArgumentException(
                $"Property '{prop.Name}' has no declaring type.", nameof(prop));
        }

        Property = prop;
        IsWritable = prop.CanWrite;
        IsReadable = prop.CanRead;
        IsString = prop.PropertyType == typeof(string);
        IsByteArray = prop.PropertyType == typeof(byte[]);
        IsDynamic = CustomAttributeExtensions
                           .GetCustomAttribute<SerializeDynamicSizeAttribute>(prop) is not null;

        DefaultValue = ComputeDefaultValue(prop.PropertyType);
        FixedSize = IsDynamic ? (ushort)0 : ComputeFixedSize(prop.PropertyType);

        // ── Getter delegate ──────────────────────────────────────────────────────
        // (T instance) => (object)instance.Prop
        // Compiled once; avoids MethodInfo.Invoke overhead and argument-array alloc.
        if (prop.CanRead && prop.GetMethod is not null)
        {
            ParameterExpression instanceParam = Expression.Parameter(typeof(object), "instance");
            UnaryExpression castInstance = Expression.Convert(instanceParam, prop.DeclaringType);
            MemberExpression propAccess = Expression.Property(castInstance, prop);
            UnaryExpression boxResult = Expression.Convert(propAccess, typeof(object));

            _getter = Expression
                          .Lambda<Func<object, object?>>(boxResult, instanceParam)
                          .Compile();
        }

        // ── Setter delegate ──────────────────────────────────────────────────────
        // (T instance, object value) => instance.Prop = (TProp)value
        // Null-safe: if value is null and property is a non-nullable value type,
        // Expression.Convert will throw at compile time — caught here at startup.
        if (prop.CanWrite && prop.SetMethod is not null)
        {
            try
            {
                ParameterExpression instanceParam = Expression.Parameter(typeof(object), "instance");
                ParameterExpression valueParam = Expression.Parameter(typeof(object), "value");
                UnaryExpression castInstance = Expression.Convert(instanceParam, prop.DeclaringType);
                UnaryExpression castValue = Expression.Convert(valueParam, prop.PropertyType);
                MemberExpression propAccess = Expression.Property(castInstance, prop);
                BinaryExpression assignExpr = Expression.Assign(propAccess, castValue);

                _setter = Expression
                              .Lambda<Action<object, object?>>(assignExpr, instanceParam, valueParam)
                              .Compile();
            }
            catch (Exception ex)
            {
                // Fail fast at startup — better than a silent NullReferenceException at runtime.
                throw new InvalidOperationException(
                    $"Failed to compile setter delegate for property '{prop.DeclaringType.Name}.{prop.Name}' " +
                    $"(type: {prop.PropertyType.Name}). " +
                    "Ensure the property type is compatible with its default value.", ex);
            }
        }
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Gets the value of this property from <paramref name="instance"/>
    /// using a compiled delegate. Returns <see langword="null"/> if no getter is available.
    /// </summary>
    /// <param name="instance">The packet object to read from. Must not be <see langword="null"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? GetValue(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return _getter?.Invoke(instance);
    }

    /// <summary>
    /// Sets the value of this property on <paramref name="instance"/>
    /// using a compiled delegate. No-op when the property is read-only.
    /// </summary>
    /// <param name="instance">The packet object to write to. Must not be <see langword="null"/>.</param>
    /// <param name="value">The value to assign. Must be compatible with the property type.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetValue(object instance, object? value)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _setter?.Invoke(instance, value);
    }

    /// <summary>
    /// Returns a human-readable description for debugging purposes.
    /// </summary>
    public override string ToString() =>
        $"{Property.DeclaringType?.Name}.{Property.Name} " +
        $"[{Property.PropertyType.Name}] " +
        $"FixedSize={FixedSize} IsDynamic={IsDynamic} IsWritable={IsWritable}";

    #endregion Public Methods

    #region Private Static Helpers

    /// <summary>
    /// Returns the wire byte-size for a fixed-width <paramref name="type"/>,
    /// or zero for unsupported / dynamic types.
    /// Enum underlying types are resolved recursively.
    /// Missing from previous version: SByte, Char, DateTime, Guid, TimeSpan, DateTimeOffset.
    /// </summary>
    /// <param name="type"></param>
    /// <exception cref="NotImplementedException"></exception>
    private static ushort ComputeFixedSize(Type type) =>
        Type.GetTypeCode(type) switch
        {
            TypeCode.Byte => 1,
            TypeCode.SByte => 1,
            TypeCode.Boolean => 1,
            TypeCode.Char => 2,
            TypeCode.Int16 => 2,
            TypeCode.UInt16 => 2,
            TypeCode.Int32 => 4,
            TypeCode.UInt32 => 4,
            TypeCode.Single => 4,
            TypeCode.Int64 => 8,
            TypeCode.UInt64 => 8,
            TypeCode.Double => 8,
            TypeCode.Decimal => 16,

            // DateTime / DateTimeOffset / TimeSpan / Guid have no TypeCode entry —
            // check by type identity before falling back to enum recursion.
            TypeCode.Object when type == typeof(Guid) => 16,
            TypeCode.Object when type == typeof(DateTime) => 8,
            TypeCode.Object when type == typeof(DateTimeOffset) => 10,
            TypeCode.Object when type == typeof(TimeSpan) => 8,
            TypeCode.Object when type == typeof(TimeOnly) => 8,
            TypeCode.Object when type == typeof(DateOnly) => 4,

            // Recursively resolve enum underlying type.
            _ when type.IsEnum => ComputeFixedSize(Enum.GetUnderlyingType(type)),
            TypeCode.Empty => throw new NotImplementedException(),
            TypeCode.DBNull => throw new NotImplementedException(),
            TypeCode.DateTime => throw new NotImplementedException(),
            TypeCode.String => throw new NotImplementedException(),

            // Unknown / reference / dynamic — caller must handle as IsDynamic.
            _ => 0
        };

    /// <summary>
    /// Returns the appropriate default / empty value for <paramref name="type"/>
    /// so that <c>ResetForPool</c> never allocates new objects on repeated calls.
    /// </summary>
    /// <param name="type"></param>
    private static object? ComputeDefaultValue(Type type)
    {
        if (type == typeof(byte[]))
        {
            return Array.Empty<byte>();
        }
        else if (type == typeof(string))
        {
            return string.Empty;
        }
        else if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }
        else
        {
            return null;
        }
    }

    #endregion Private Static Helpers
}
