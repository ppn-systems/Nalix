// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Serialization.Attributes;

namespace Nalix.Shared.Frames;

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
    /// Pre-computed fixed byte-size of this property.
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
    /// Used by <c>ComputeDynamicLength</c> to call
    /// <see cref="System.Text.Encoding.UTF8"/> byte-count instead of char-count.
    /// </summary>
    public System.Boolean IsString { get; }

    /// <summary>
    /// Cached result of <see cref="System.Reflection.PropertyInfo.CanWrite"/>.
    /// </summary>
    public System.Boolean IsWritable { get; }

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

    // Compiled open-instance delegates — avoids boxing overhead of PropertyInfo.GetValue/SetValue.
    // "Open-instance" means the first argument is the target object.
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

        Property = prop;
        IsWritable = prop.CanWrite;
        IsString = prop.PropertyType == typeof(System.String);

        IsDynamic = System.Reflection.CustomAttributeExtensions
                          .GetCustomAttribute<SerializeDynamicSizeAttribute>(prop) is not null;

        FixedSize = IsDynamic ? (System.UInt16)0 : ComputeFixedSize(prop.PropertyType);
        DefaultValue = ComputeDefaultValue(prop.PropertyType);

        // Compile open-instance delegates once.
        // CreateDelegate with a null first argument creates an open delegate where
        // the first parameter becomes the target instance.
        if (prop.CanRead && prop.GetMethod is not null)
        {
            System.Reflection.MethodInfo getter = prop.GetMethod;
            _getter = instance => getter.Invoke(instance, null);
        }

        if (prop.CanWrite && prop.SetMethod is not null)
        {
            System.Reflection.MethodInfo setter = prop.SetMethod;
            _setter = (instance, value) => setter.Invoke(instance, [value]);
        }
    }

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Gets the value of this property from <paramref name="instance"/>
    /// using a pre-compiled delegate instead of <see cref="System.Reflection.PropertyInfo.GetValue(System.Object)"/>.
    /// </summary>
    /// <param name="instance">The packet object to read from.</param>
    /// <returns>The current property value, or <see langword="null"/> if no getter exists.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Object? GetValue(System.Object instance) => _getter?.Invoke(instance);

    /// <summary>
    /// Sets the value of this property on <paramref name="instance"/>
    /// using a pre-compiled delegate instead of <see cref="System.Reflection.PropertyInfo.SetValue(System.Object, System.Object)"/>.
    /// No-op when the property is read-only.
    /// </summary>
    /// <param name="instance">The packet object to write to.</param>
    /// <param name="value">The value to assign.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetValue(System.Object instance, System.Object? value) => _setter?.Invoke(instance, value);

    #endregion Public Methods

    #region Private Static Helpers

    /// <summary>
    /// Returns the wire byte-size for a fixed-width <paramref name="type"/>,
    /// or zero for unsupported / dynamic types.
    /// Enum underlying types are resolved recursively.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt16 ComputeFixedSize(System.Type type) =>
        System.Type.GetTypeCode(type) switch
        {
            System.TypeCode.Byte => 1,
            System.TypeCode.Boolean => 1,
            System.TypeCode.Int16 => 2,
            System.TypeCode.UInt16 => 2,
            System.TypeCode.Int32 => 4,
            System.TypeCode.UInt32 => 4,
            System.TypeCode.Int64 => 8,
            System.TypeCode.UInt64 => 8,
            System.TypeCode.Single => 4,
            System.TypeCode.Double => 8,
            System.TypeCode.Decimal => 16,
            _ => type.IsEnum
                    ? ComputeFixedSize(System.Enum.GetUnderlyingType(type))
                    : (System.UInt16)0
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