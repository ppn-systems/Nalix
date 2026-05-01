// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;
using Nalix.Codec.Serialization.Internal.Types;

namespace Nalix.Codec.DataFrames.Internal;

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
    /// Zero when unknown / variable-sized.
    /// </summary>
    public ushort FixedSize { get; }

    /// <summary>
    /// <see langword="true"/> when the property is annotated with
    /// <see cref="SerializeDynamicSizeAttribute"/>, meaning its wire-size
    /// must be evaluated at runtime.
    /// </summary>
    public bool IsDynamic { get; }

    /// <summary>
    /// Cached result of <see cref="PropertyInfo.CanWrite"/> + additional skip rules.
    /// </summary>
    public bool IsWritable { get; }

    /// <summary>
    /// <see langword="true"/> when this property has a public getter.
    /// </summary>
    public bool IsReadable { get; }

    /// <summary>
    /// Pre-computed default value used by <c>ResetForPool</c>.
    /// </summary>
    public object? DefaultValue { get; }

    /// <summary>
    /// The declared type of this property.
    /// </summary>
    public Type DeclaredType { get; }

    /// <summary>
    /// Null wire-size for this property based on its declared type and the serializer's wire format.
    /// </summary>
    public int NullWireSize { get; }

    /// <summary>
    /// A cached classification to select the fastest sizing path at runtime.
    /// </summary>
    public DynamicWireKind DynamicKind { get; }

    /// <summary>
    /// Element size for unmanaged arrays. Zero for non-array properties.
    /// </summary>
    public int ElementSize { get; }

    #endregion Public Properties

    #region Private Fields

    /// <summary>
    /// Compiled open-instance getter delegate.
    /// Note: returns <see cref="object"/> so value types will box.
    /// We avoid calling this delegate for fixed-size properties in hot paths.
    /// </summary>
    private readonly Func<object, object?>? _getter;

    private readonly Action<object, object?>? _setter;

    #endregion Private Fields

    #region Constructor

    /// <summary>
    /// Initializes a new <see cref="PropertyMetadata"/> by reflecting on <paramref name="prop"/>
    /// and caching all derived information.
    /// </summary>
    public PropertyMetadata(PropertyInfo prop)
    {
        ArgumentNullException.ThrowIfNull(prop);

        if (prop.DeclaringType is null)
        {
            throw new ArgumentException(
                $"Property '{prop.Name}' has no declaring type.", nameof(prop));
        }

        this.Property = prop;
        this.DeclaredType = prop.PropertyType;

        this.IsWritable =
            prop.CanWrite
            && prop.SetMethod is { IsPublic: true }
            && !(CustomAttributeExtensions.GetCustomAttribute<SkipCleanAttribute>(prop) is not null);

        this.IsReadable = prop.CanRead;

        SerializeDynamicSizeAttribute? dynAttr = CustomAttributeExtensions.GetCustomAttribute<SerializeDynamicSizeAttribute>(prop);
        this.IsDynamic = dynAttr is not null && dynAttr.Size == 0;

        this.NullWireSize = ComputeNullWireSize(this.DeclaredType);
        (this.DynamicKind, this.ElementSize) = ComputeDynamicKind(this.DeclaredType);

        this.DefaultValue = ComputeDefaultValue(this.DeclaredType);
        this.FixedSize = dynAttr is not null && dynAttr.Size > 0
            ? (ushort)dynAttr.Size
            : (this.IsDynamic ? (ushort)0 : ComputeFixedSize(this.DeclaredType));

        // Getter delegate: (object instance) => (object) ((TDeclaring)instance).Prop
        if (prop.CanRead && prop.GetMethod is not null)
        {
            ParameterExpression instanceParam = Expression.Parameter(typeof(object), "instance");
            UnaryExpression castInstance = Expression.Convert(instanceParam, prop.DeclaringType);
            MemberExpression propAccess = Expression.Property(castInstance, prop);
            UnaryExpression boxResult = Expression.Convert(propAccess, typeof(object));

            _getter = Expression.Lambda<Func<object, object?>>(boxResult, instanceParam).Compile();
        }

        // Setter delegate: (object instance, object value) => ((TDeclaring)instance).Prop = (TProp)value
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

                _setter = Expression.Lambda<Action<object, object?>>(assignExpr, instanceParam, valueParam).Compile();
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                throw new InternalErrorException(
                    $"Failed to compile setter delegate for property '{prop.DeclaringType.Name}.{prop.Name}' " +
                    $"(type: {prop.PropertyType.Name}). Ensure the property type is compatible with its default value.",
                    ex);
            }
        }
    }

    #endregion Constructor

    #region Public Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? GetValue(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return _getter?.Invoke(instance);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetValue(object instance, object? value)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _setter?.Invoke(instance, value);
    }

    public override string ToString() =>
        $"{this.Property.DeclaringType?.Name}.{this.Property.Name} " +
        $"[{this.Property.PropertyType.Name}] FixedSize={this.FixedSize} IsDynamic={this.IsDynamic} IsWritable={this.IsWritable}";

    #endregion Public Methods

    #region Private Static Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeNullWireSize(Type declaredType)
    {
        // Nullable<T>: 1-byte presence flag.
        if (Nullable.GetUnderlyingType(declaredType) is not null)
        {
            return sizeof(byte);
        }

        // Strings and arrays use an Int32 sentinel (-1) in the current wire format.
        if (declaredType == typeof(string) || declaredType.IsArray)
        {
            return sizeof(int);
        }

        // Other reference types use NullableObjectFormatter<T>, which writes a 1-byte marker.
        if (!declaredType.IsValueType)
        {
            return sizeof(byte);
        }

        // Value types do not use a null marker, but keep the legacy fallback for unsupported cases.
        return sizeof(int);
    }

    private static (DynamicWireKind kind, int elementSize) ComputeDynamicKind(Type declaredType)
    {
        if (declaredType == typeof(string))
        {
            return (DynamicWireKind.String, 0);
        }

        if (declaredType == typeof(byte[]))
        {
            return (DynamicWireKind.ByteArray, 0);
        }

        if (typeof(IPacket).IsAssignableFrom(declaredType))
        {
            return (DynamicWireKind.Packet, 0);
        }

        if (declaredType.IsArray)
        {
            Type? elementType = declaredType.GetElementType();
            if (elementType is not null && TypeMetadata.IsUnmanaged(elementType))
            {
                return (DynamicWireKind.UnmanagedArray, PacketBaseElementSizer.GetElementSize(elementType));
            }

            return (DynamicWireKind.Other, 0);
        }

        return (DynamicWireKind.Other, 0);
    }

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
            TypeCode.DateTime => 8,

            TypeCode.Object when type == typeof(Guid) => 16,
            TypeCode.Object when type == typeof(TimeSpan) => 8,
            TypeCode.Object when type == typeof(TimeOnly) => 8,
            TypeCode.Object when type == typeof(DateOnly) => 4,
            TypeCode.Object when type == typeof(DateTimeOffset) => 10,

            _ when type.IsEnum => ComputeFixedSize(Enum.GetUnderlyingType(type)),
            TypeCode.Empty => 0,
            TypeCode.DBNull => 0,
            TypeCode.String => 0,
            _ => 0
        };

    private static object? ComputeDefaultValue(Type type)
    {
        if (type == typeof(byte[]))
        {
            return Array.Empty<byte>();
        }

        if (type == typeof(string))
        {
            return string.Empty;
        }

        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }

        return null;
    }

    #endregion Private Static Helpers
}
