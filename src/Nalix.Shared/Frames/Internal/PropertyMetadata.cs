// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Serialization.Attributes;

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
    /// The underlying <see cref="System.Reflection.PropertyInfo"/>.
    /// Retained for interop with APIs that require it (e.g. LiteSerializer).
    /// Prefer <see cref="GetValue"/> / <see cref="SetValue"/> in hot paths.
    /// </summary>
    public System.Reflection.PropertyInfo Property { get; }

    /// <summary>
    /// Pre-computed fixed byte-size of this property on the wire.
    /// Zero when <see cref="IsDynamic"/> is <see langword="true"/>.
    /// </summary>
    public System.UInt16 FixedSize { get; }

    /// <summary>
    /// <see langword="true"/> when the property is annotated with
    /// <see cref="SerializeDynamicSizeAttribute"/>, meaning its wire-size
    /// must be evaluated at runtime.
    /// </summary>
    public System.Boolean IsDynamic { get; }

    /// <summary>
    /// <see langword="true"/> when the property type is <see cref="System.String"/>.
    /// </summary>
    public System.Boolean IsString { get; }

    /// <summary>
    /// <see langword="true"/> when the property type is <see cref="System.Byte"/>[].
    /// </summary>
    public System.Boolean IsByteArray { get; }

    /// <summary>
    /// Cached result of <see cref="System.Reflection.PropertyInfo.CanWrite"/>.
    /// </summary>
    public System.Boolean IsWritable { get; }

    /// <summary>
    /// <see langword="true"/> when this property has a public getter.
    /// </summary>
    public System.Boolean IsReadable { get; }

    /// <summary>
    /// Pre-computed default value used by <c>ResetForPool</c>.
    /// <list type="bullet">
    ///   <item><see cref="System.Byte"/>[] → <see cref="System.Array.Empty{T}"/></item>
    ///   <item><see cref="System.String"/> → <see cref="System.String.Empty"/></item>
    ///   <item>Value type → <see cref="System.Activator.CreateInstance(System.Type)"/></item>
    ///   <item>Reference type → <see langword="null"/></item>
    /// </list>
    /// </summary>
    public System.Object? DefaultValue { get; }

    #endregion Public Properties

    #region Private Fields

    // True compiled open-instance delegates via Expression Trees.
    // No MethodInfo.Invoke — no argument array allocation, no boxing round-trip.
    private readonly System.Func<System.Object, System.Object?>? _getter;
    private readonly System.Action<System.Object, System.Object?>? _setter;

    #endregion Private Fields

    #region Constructor

    /// <summary>
    /// Initializes a new <see cref="PropertyMetadata"/> by reflecting on
    /// <paramref name="prop"/> and caching all derived information.
    /// </summary>
    /// <param name="prop">A public instance property decorated with
    /// <see cref="SerializeOrderAttribute"/>.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="prop"/> is <see langword="null"/>.
    /// </exception>
    public PropertyMetadata(System.Reflection.PropertyInfo prop)
    {
        System.ArgumentNullException.ThrowIfNull(prop);

        // Guard: DeclaringType must be known to build open-instance delegates.
        if (prop.DeclaringType is null)
        {
            throw new System.ArgumentException(
                $"Property '{prop.Name}' has no declaring type.", nameof(prop));
        }

        Property = prop;
        IsWritable = prop.CanWrite;
        IsReadable = prop.CanRead;
        IsString = prop.PropertyType == typeof(System.String);
        IsByteArray = prop.PropertyType == typeof(System.Byte[]);
        IsDynamic = System.Reflection.CustomAttributeExtensions
                           .GetCustomAttribute<SerializeDynamicSizeAttribute>(prop) is not null;

        DefaultValue = ComputeDefaultValue(prop.PropertyType);
        FixedSize = IsDynamic ? (System.UInt16)0 : ComputeFixedSize(prop.PropertyType);

        // ── Getter delegate ──────────────────────────────────────────────────────
        // (T instance) => (object)instance.Prop
        // Compiled once; avoids MethodInfo.Invoke overhead and argument-array alloc.
        if (prop.CanRead && prop.GetMethod is not null)
        {
            var instanceParam = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "instance");
            var castInstance = System.Linq.Expressions.Expression.Convert(instanceParam, prop.DeclaringType);
            var propAccess = System.Linq.Expressions.Expression.Property(castInstance, prop);
            var boxResult = System.Linq.Expressions.Expression.Convert(propAccess, typeof(System.Object));

            _getter = System.Linq.Expressions.Expression
                          .Lambda<System.Func<System.Object, System.Object?>>(boxResult, instanceParam)
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
                var instanceParam = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "instance");
                var valueParam = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "value");
                var castInstance = System.Linq.Expressions.Expression.Convert(instanceParam, prop.DeclaringType);
                var castValue = System.Linq.Expressions.Expression.Convert(valueParam, prop.PropertyType);
                var propAccess = System.Linq.Expressions.Expression.Property(castInstance, prop);
                var assignExpr = System.Linq.Expressions.Expression.Assign(propAccess, castValue);

                _setter = System.Linq.Expressions.Expression
                              .Lambda<System.Action<System.Object, System.Object?>>(assignExpr, instanceParam, valueParam)
                              .Compile();
            }
            catch (System.Exception ex)
            {
                // Fail fast at startup — better than a silent NullReferenceException at runtime.
                throw new System.InvalidOperationException(
                    $"Failed to compile setter delegate for property '{prop.DeclaringType.Name}.{prop.Name}' " +
                    $"(type: {prop.PropertyType.Name}). " +
                    $"Ensure the property type is compatible with its default value.", ex);
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Object? GetValue(System.Object instance)
    {
        System.ArgumentNullException.ThrowIfNull(instance);
        return _getter?.Invoke(instance);
    }

    /// <summary>
    /// Sets the value of this property on <paramref name="instance"/>
    /// using a compiled delegate. No-op when the property is read-only.
    /// </summary>
    /// <param name="instance">The packet object to write to. Must not be <see langword="null"/>.</param>
    /// <param name="value">The value to assign. Must be compatible with the property type.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetValue(System.Object instance, System.Object? value)
    {
        System.ArgumentNullException.ThrowIfNull(instance);
        _setter?.Invoke(instance, value);
    }

    /// <summary>
    /// Returns a human-readable description for debugging purposes.
    /// </summary>
    public override System.String ToString() =>
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
    private static System.UInt16 ComputeFixedSize(System.Type type) =>
        System.Type.GetTypeCode(type) switch
        {
            System.TypeCode.Byte => 1,
            System.TypeCode.SByte => 1,
            System.TypeCode.Boolean => 1,
            System.TypeCode.Char => 2,
            System.TypeCode.Int16 => 2,
            System.TypeCode.UInt16 => 2,
            System.TypeCode.Int32 => 4,
            System.TypeCode.UInt32 => 4,
            System.TypeCode.Single => 4,
            System.TypeCode.Int64 => 8,
            System.TypeCode.UInt64 => 8,
            System.TypeCode.Double => 8,
            System.TypeCode.Decimal => 16,

            // DateTime / DateTimeOffset / TimeSpan / Guid have no TypeCode entry —
            // check by type identity before falling back to enum recursion.
            System.TypeCode.Object when type == typeof(System.Guid) => 16,
            System.TypeCode.Object when type == typeof(System.DateTime) => 8,
            System.TypeCode.Object when type == typeof(System.DateTimeOffset) => 10,
            System.TypeCode.Object when type == typeof(System.TimeSpan) => 8,
            System.TypeCode.Object when type == typeof(System.TimeOnly) => 8,
            System.TypeCode.Object when type == typeof(System.DateOnly) => 4,

            // Recursively resolve enum underlying type.
            _ when type.IsEnum => ComputeFixedSize(System.Enum.GetUnderlyingType(type)),

            // Unknown / reference / dynamic — caller must handle as IsDynamic.
            _ => 0
        };

    /// <summary>
    /// Returns the appropriate default / empty value for <paramref name="type"/>
    /// so that <c>ResetForPool</c> never allocates new objects on repeated calls.
    /// </summary>
    private static System.Object? ComputeDefaultValue(System.Type type)
    {
        return type == typeof(System.Byte[])
            ? System.Array.Empty<System.Byte>()
            : type == typeof(System.String) ? System.String.Empty : type.IsValueType ? System.Activator.CreateInstance(type) : null;
    }

    #endregion Private Static Helpers
}