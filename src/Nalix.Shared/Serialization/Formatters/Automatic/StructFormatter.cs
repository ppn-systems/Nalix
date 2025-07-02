using Nalix.Common.Exceptions;
using Nalix.Common.Logging;
using Nalix.Shared.Serialization.Buffers;
using Nalix.Shared.Serialization.Internal.Accessors;
using Nalix.Shared.Serialization.Internal.Reflection;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Nalix.Shared.Serialization.Formatters.Automatic;

/// <summary>
/// Optimized field-based serializer eliminating boxing for maximum performance.
/// Implements SOLID principles with Domain-Driven Design patterns.
/// </summary>
/// <typeparam name="T">The type to serialize.</typeparam>
public sealed class StructFormatter<T> : IFormatter<T>, IDisposable where T : struct
{
    #region Core Fields

    /// <summary>
    /// Array of cached field accessors for optimized serialization performance.
    /// </summary>
    private readonly FieldAccessor<T>[] _accessors;

    /// <summary>
    /// Logger instance for tracking serialization diagnostics.
    /// </summary>
    private readonly ILogger? _logger = null;

    /// <summary>
    /// Activity tracking source for tracing serialization operations.
    /// </summary>
    private static readonly ActivitySource _activitySource = new($"Nalix.Serialization.{typeof(T).Name}");

    /// <summary>
    /// Indicates whether the formatter has been disposed.
    /// </summary>
    private bool _disposed;

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
            _accessors = CreateAccessors();
            _logger?.Info("ObjectFormatter<{Type}> initialized: {Count} fields, {Layout} layout",
                typeof(T).Name, _accessors.Length, FieldCache<T>.GetLayout());
        }
        catch (Exception ex)
        {
            _logger?.Error("Failed to initialize ObjectFormatter<{Type}>: {Error}", typeof(T).Name, ex.Message);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var activity = _activitySource.StartActivity($"Serialize.{typeof(T).Name}");

        try
        {
            for (int i = 0; i < _accessors.Length; i++)
            {
                _accessors[i].Serialize(ref writer, value);
            }
        }
        catch (Exception ex) when (ex is not SerializationException)
        {
            throw new SerializationException($"Serialization failed for {typeof(T).Name}", ex);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Deserialize(ref DataReader reader)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var activity = _activitySource.StartActivity($"Deserialize.{typeof(T).Name}");

        try
        {
            var obj = Activator.CreateInstance<T>();

            for (int i = 0; i < _accessors.Length; i++)
            {
                _accessors[i].Deserialize(ref reader, obj);
            }

            return obj;
        }
        catch (Exception ex) when (ex is not SerializationException)
        {
            throw new SerializationException($"Deserialization failed for {typeof(T).Name}", ex);
        }
    }

    #endregion Serialization

    #region Private Implementation

    /// <summary>
    /// Creates field accessors for the specified type.
    /// </summary>
    /// <returns>An array of field accessors.</returns>
    private FieldAccessor<T>[] CreateAccessors()
    {
        var fields = FieldCache<T>.GetFields();
        if (fields.Length is 0) return [];

        var accessors = new FieldAccessor<T>[fields.Length];

        for (int i = 0; i < fields.Length; i++)
        {
            try
            {
                accessors[i] = FieldAccessor<T>.Create(fields[i], i);
            }
            catch (Exception ex)
            {
                _logger?.Warn("Skipping field {Field}: {Error}", fields[i].Name, ex.Message);
                throw;
            }
        }

        return accessors;
    }

    #endregion Private Implementation

    #region Disposal

    /// <summary>
    /// Disposes of the formatter, releasing any allocated resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _activitySource.Dispose();
        _disposed = true;
    }

    #endregion Disposal
}