// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Common.Logging;
using Nalix.Framework.Injection;
using Nalix.Shared.Serialization.Buffers;
using Nalix.Shared.Serialization.Internal.Accessors;
using Nalix.Shared.Serialization.Internal.Reflection;

namespace Nalix.Shared.Serialization.Formatters.Automatic;

/// <summary>
/// Optimized field-based serializer eliminating boxing for maximum performance.
/// Implements SOLID principles with Domain-Driven Design patterns.
/// </summary>
/// <typeparam name="T">The type to serialize.</typeparam>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class StructFormatter<T> : IFormatter<T> where T : struct
{
    #region Core Fields

    private static System.String DebuggerDisplay => $"StructFormatter<{typeof(T).FullName}>";

    /// <summary>
    /// Array of cached field accessors for optimized serialization performance.
    /// </summary>
    private readonly FieldAccessor<T>[] _accessors;

    #endregion Core Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="ObjectFormatter{T}"/>.
    /// </summary>
    /// <exception cref="SerializationException">
    /// Thrown if initialization of property accessors fails.
    /// </exception>
    public StructFormatter()
    {
        try
        {
            _accessors = StructFormatter<T>.CreateAccessors();
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[StructFormatter<{typeof(T).Name}>] " +
                                           $"init-ok fields={_accessors.Length} layout={FieldCache<T>.GetLayout()}");
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[StructFormatter<{typeof(T).Name}>] " +
                                           $"init-fail msg={ex.Message}");

            throw new SerializationException($"Formatter initialization failed for {typeof(T).Name}", ex);
        }
    }

    #endregion Constructors

    #region Serialization

    /// <summary>
    /// Serializes an object into the provided binary writer.
    /// </summary>
    /// <param name="writer">The binary writer used for serialization.</param>
    /// <param name="value">The object to serialize.</param>
    /// <exception cref="SerializationException">
    /// Thrown if serialization encounters an error.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T value)
    {
        for (System.Int32 i = 0; i < _accessors.Length; i++)
        {
            _accessors[i].Serialize(ref writer, value);
        }
    }

    /// <summary>
    /// Deserializes an object from the provided binary reader.
    /// </summary>
    /// <param name="reader">The binary reader containing serialized data.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if deserialization encounters an error.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T Deserialize(ref DataReader reader)
    {
        T obj = System.Activator.CreateInstance<T>();

        for (System.Int32 i = 0; i < _accessors.Length; i++)
        {
            _accessors[i].Deserialize(ref reader, obj);
        }

        return obj;
    }

    #endregion Serialization

    #region Private Implementation

    /// <summary>
    /// Creates field accessors for the specified type.
    /// </summary>
    /// <returns>An array of field accessors.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static FieldAccessor<T>[] CreateAccessors()
    {
        var fields = FieldCache<T>.GetFields();
        if (fields.Length is 0)
        {
            return [];
        }

        var accessors = new FieldAccessor<T>[fields.Length];

        for (System.Int32 i = 0; i < fields.Length; i++)
        {
            accessors[i] = FieldAccessor<T>.Create(fields[i], i);
        }

        return accessors;
    }

    #endregion Private Implementation
}