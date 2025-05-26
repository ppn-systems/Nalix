using Nalix.Common.Exceptions;
using Nalix.Common.Logging;
using Nalix.Serialization.Buffers;
using Nalix.Serialization.Formatters;
using Nalix.Serialization.Internal.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace Nalix.Serialization.Automatic;

/// <summary>
/// Provides an optimized automatic serializer for struct types, eliminating boxing
/// and achieving near hand-written serialization performance.
/// Implements SOLID principles and follows Domain-Driven Design patterns.
/// </summary>
/// <typeparam name="T">The struct type to serialize.</typeparam>
public sealed class StructFormatter<T> : IFormatter<T>, IDisposable where T : struct
{
    #region Fields and Properties

    /// <summary>
    /// Cached array of property accessors for efficient serialization.
    /// </summary>
    private readonly PropertyAccessor[] _accessors;

    /// <summary>
    /// Configuration options for serialization behavior.
    /// </summary>
    private readonly SerializationCode _options;

    /// <summary>
    /// Logger for diagnostic and error tracking.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Activity source for telemetry and performance monitoring.
    /// </summary>
    private static readonly ActivitySource ActivitySource = new($"Nalix.Serialization.Struct.{typeof(T).Name}");

    /// <summary>
    /// Indicates whether this instance has been disposed.
    /// </summary>
    private bool _disposed;

    #endregion Fields and Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="StructFormatter{T}"/> with default options.
    /// </summary>
    public StructFormatter() : this(SerializationCode.Default, null) { }

    /// <summary>
    /// Initializes a new instance of <see cref="StructFormatter{T}"/> with custom options.
    /// </summary>
    /// <param name="options">Serialization configuration options.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public StructFormatter(SerializationCode options, ILogger logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        try
        {
            _accessors = CreatePropertyAccessors();
            _logger?.Info("StructFormatter<{0}> initialized with {1} properties", typeof(T).Name, _accessors.Length);
        }
        catch (Exception ex)
        {
            _logger?.Error($"Failed to initialize StructFormatter<{typeof(T).Name}>", ex);
            throw new SerializationException($"Failed to initialize formatter for struct type {typeof(T).Name}", ex);
        }
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Serializes the given struct into the provided writer with comprehensive error handling.
    /// </summary>
    /// <param name="writer">The writer used for serialization.</param>
    /// <param name="value">The struct to serialize.</param>
    /// <exception cref="ObjectDisposedException">Thrown when formatter has been disposed.</exception>
    /// <exception cref="SerializationException">Thrown when serialization fails.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T value)
    {
        ThrowIfDisposed();

        using var activity = ActivitySource.StartActivity($"Serialize.{typeof(T).Name}");

        try
        {
            // Serialize all properties - no null checks needed for structs
            for (int i = 0; i < _accessors.Length; i++)
            {
                _accessors[i].Serialize(ref writer, value);
            }

            _logger?.Debug("Successfully serialized struct of type {0}", typeof(T).Name);
        }
        catch (Exception ex) when (ex is not SerializationException)
        {
            var serializationEx = new SerializationException($"Failed to serialize struct of type {typeof(T).Name}", ex);
            _logger?.Error("Serialization failed for struct type {0}: {1}", typeof(T).Name, serializationEx.Message);
            throw serializationEx;
        }
    }

    /// <summary>
    /// Deserializes a struct from the provided reader with comprehensive validation.
    /// </summary>
    /// <param name="reader">The reader containing serialized data.</param>
    /// <returns>The deserialized struct.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when formatter has been disposed.</exception>
    /// <exception cref="SerializationException">Thrown when deserialization fails.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T Deserialize(ref DataReader reader)
    {
        ThrowIfDisposed();

        using var activity = ActivitySource.StartActivity($"Deserialize.{typeof(T).Name}");

        try
        {
            // Create default struct instance
            var obj = new T();

            // Deserialize all properties
            for (int i = 0; i < _accessors.Length; i++)
            {
                _accessors[i].Deserialize(ref reader, ref obj);
            }

            _logger?.Debug("Successfully deserialized struct of type {0}", typeof(T).Name);
            return obj;
        }
        catch (Exception ex) when (ex is not SerializationException)
        {
            var serializationEx = new SerializationException($"Failed to deserialize struct of type {typeof(T).Name}", ex);
            _logger?.Error("Deserialization failed for struct type {0}: {1}", typeof(T).Name, serializationEx.Message);
            throw serializationEx;
        }
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Creates property accessors following the Open/Closed Principle.
    /// </summary>
    /// <returns>Array of property accessors.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality",
        "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming",
        "IL2087:Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. " +
        "The generic parameter of the source method or type does not have matching annotations.", Justification = "<Pending>")]
    private PropertyAccessor[] CreatePropertyAccessors()
    {
        var properties = TypeMetadata.GetProperties(typeof(T));
        var accessors = new List<PropertyAccessor>(properties.Length);

        foreach (var property in properties)
        {
            try
            {
                var accessor = PropertyAccessor.Create(property, _options);
                accessors.Add(accessor);
            }
            catch (Exception ex)
            {
                if (_options.FailOnPropertyErrors)
                    throw;

                _logger?.Warn("Skipping property {0} due to error: {1}", property.Name, ex.Message);
            }
        }

        return [.. accessors];
    }

    /// <summary>
    /// Throws ObjectDisposedException if this instance has been disposed.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, nameof(StructFormatter<T>));

    #endregion Private Methods

    #region IDisposable Implementation

    /// <summary>
    /// Releases all resources used by the StructFormatter.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            // Dispose property accessors if they implement IDisposable
            for (int i = 0; i < _accessors.Length; i++)
            {
                if (_accessors[i] is IDisposable disposableAccessor)
                {
                    disposableAccessor.Dispose();
                }
            }

            ActivitySource.Dispose();
        }
        catch (Exception ex)
        {
            _logger?.Warn("Error during StructFormatter disposal: {0}", ex.Message);
        }
        finally
        {
            _disposed = true;
            _logger?.Debug("StructFormatter<{0}> disposed", typeof(T).Name);
        }
    }

    #endregion IDisposable Implementation

    #region Nested Types

    /// <summary>
    /// Abstract base class for struct property serialization following the Strategy Pattern.
    /// Implements the Open/Closed Principle for extensibility.
    /// </summary>
    private abstract class PropertyAccessor
    {
        /// <summary>
        /// Gets the name of the property this accessor handles.
        /// </summary>
        public abstract string PropertyName { get; }

        /// <summary>
        /// Serializes a property of the given struct.
        /// </summary>
        /// <param name="writer">The writer used for serialization.</param>
        /// <param name="obj">The struct containing the property.</param>
        public abstract void Serialize(ref DataWriter writer, T obj);

        /// <summary>
        /// Deserializes a property into the given struct.
        /// Note: struct is passed by reference to allow modification.
        /// </summary>
        /// <param name="reader">The reader containing serialized data.</param>
        /// <param name="obj">The struct to populate with deserialized data.</param>
        public abstract void Deserialize(ref DataReader reader, ref T obj);

        /// <summary>
        /// Factory method that creates a strongly typed property accessor.
        /// Implements the Factory Pattern for type-safe creation.
        /// </summary>
        /// <param name="property">The property to generate an accessor for.</param>
        /// <param name="options">Serialization options.</param>
        /// <returns>A specialized property accessor.</returns>
        /// <exception cref="ArgumentNullException">Thrown when property or options is null.</exception>
        /// <exception cref="SerializationException">Thrown when accessor creation fails.</exception>
        public static PropertyAccessor Create(PropertyInfo property, SerializationCode options)
        {
            ArgumentNullException.ThrowIfNull(property, nameof(property));
            ArgumentNullException.ThrowIfNull(options, nameof(options));

            try
            {
                // Sử dụng reflection để gọi generic method helper
                var createMethod = typeof(PropertyAccessor)
                    .GetMethod(nameof(CreateGeneric), BindingFlags.NonPublic | BindingFlags.Static);

                if (createMethod is null)
                {
                    throw new InvalidOperationException("CreateGeneric method not found");
                }

                var genericMethod = createMethod.MakeGenericMethod(property.PropertyType);
                var result = genericMethod.Invoke(null, [property, options]);

                return (PropertyAccessor)result!;
            }
            catch (Exception ex)
            {
                throw new SerializationException($"Failed to create struct accessor for property {property.Name}", ex);
            }
        }

        /// <summary>
        /// Generic helper method để tạo PropertyAccessorImpl
        /// </summary>
        private static PropertyAccessor CreateGeneric<TProp>(PropertyInfo property, SerializationCode options)
        {
            return new PropertyAccessorImpl<TProp>(property, options);
        }
    }

    /// <summary>
    /// Strongly-typed property accessor implementation for structs that eliminates boxing.
    /// Follows the Interface Segregation Principle with focused responsibilities.
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    private sealed class PropertyAccessorImpl<TProp> : PropertyAccessor, IDisposable
    {
        #region Fields

        private readonly string _propertyName;
        private readonly Func<T, TProp> _getter;
        private readonly StructSetter _setter;
        private readonly IFormatter<TProp> _formatter;
        private readonly SerializationCode _options;
        private bool _disposed;

        #endregion Fields

        #region Delegates

        /// <summary>
        /// Delegate for setting struct properties by reference to avoid boxing.
        /// </summary>
        /// <param name="obj">The struct instance passed by reference.</param>
        /// <param name="value">The value to set.</param>
        private delegate void StructSetter(ref T obj, TProp value);

        #endregion Delegates

        #region Properties

        /// <summary>
        /// Gets the name of the property this accessor handles.
        /// </summary>
        public override string PropertyName => _propertyName;

        #endregion Properties

        #region Constructor

        /// <summary>
        /// Initializes a new property accessor with compile-time optimizations for structs.
        /// </summary>
        /// <param name="property">The property information.</param>
        /// <param name="options">Serialization options.</param>
        public PropertyAccessorImpl(PropertyInfo property, SerializationCode options)
        {
            ArgumentNullException.ThrowIfNull(property, nameof(property));
            ArgumentNullException.ThrowIfNull(options, nameof(options));

            _propertyName = property.Name;
            _options = options;

            try
            {
                _getter = CreateOptimizedGetter(property);
                _setter = CreateOptimizedStructSetter(property);
                _formatter = FormatterProvider.Get<TProp>();
            }
            catch (Exception ex)
            {
                throw new SerializationException($"Failed to create struct accessor for property {property.Name}", ex);
            }
        }

        #endregion Constructor

        #region Public Methods

        /// <summary>
        /// Serializes a property using type-safe, optimized access without boxing.
        /// </summary>
        /// <param name="writer">The writer used for serialization.</param>
        /// <param name="obj">The struct containing the property.</param>
        /// <exception cref="SerializationException">Thrown when serialization fails.</exception>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public override void Serialize(ref DataWriter writer, T obj)
        {
            ThrowIfDisposed();

            try
            {
                var value = _getter(obj);
                _formatter.Serialize(ref writer, value);
            }
            catch (Exception ex)
            {
                throw new SerializationException($"Failed to serialize struct property {_propertyName}", ex);
            }
        }

        /// <summary>
        /// Deserializes a property value using type-safe, optimized access without boxing.
        /// </summary>
        /// <param name="reader">The reader containing serialized data.</param>
        /// <param name="obj">The struct to populate with deserialized data (passed by reference).</param>
        /// <exception cref="SerializationException">Thrown when deserialization fails.</exception>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public override void Deserialize(ref DataReader reader, ref T obj)
        {
            ThrowIfDisposed();

            try
            {
                var value = _formatter.Deserialize(ref reader);
                _setter(ref obj, value);
            }
            catch (Exception ex)
            {
                throw new SerializationException($"Failed to deserialize struct property {_propertyName}", ex);
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Creates an optimized getter using expression trees for structs.
        /// </summary>
        /// <param name="property">The property to create a getter for.</param>
        /// <returns>Compiled getter function.</returns>
        private static Func<T, TProp> CreateOptimizedGetter(PropertyInfo property)
        {
            if (!property.CanRead)
                throw new ArgumentException($"Property {property.Name} is not readable", nameof(property));

            try
            {
                var param = Expression.Parameter(typeof(T), "obj");
                var propertyAccess = Expression.Property(param, property);

                // Handle type conversion if necessary
                Expression body = propertyAccess;
                if (property.PropertyType != typeof(TProp))
                {
                    body = Expression.Convert(propertyAccess, typeof(TProp));
                }

                var lambda = Expression.Lambda<Func<T, TProp>>(body, param);
                return lambda.Compile();
            }
            catch (Exception ex)
            {
                throw new SerializationException($"Failed to create struct getter for property {property.Name}", ex);
            }
        }

        /// <summary>
        /// Creates an optimized setter using expression trees for structs passed by reference.
        /// </summary>
        /// <param name="property">The property to create a setter for.</param>
        /// <returns>Compiled setter delegate.</returns>
        private static StructSetter CreateOptimizedStructSetter(PropertyInfo property)
        {
            if (!property.CanWrite)
                throw new ArgumentException($"Property {property.Name} is not writable", nameof(property));

            try
            {
                var objParam = Expression.Parameter(typeof(T).MakeByRefType(), "obj");
                var valueParam = Expression.Parameter(typeof(TProp), "value");

                var propertyAccess = Expression.Property(objParam, property);

                // Handle type conversion if necessary
                Expression valueExpression = valueParam;
                if (typeof(TProp) != property.PropertyType)
                {
                    valueExpression = Expression.Convert(valueParam, property.PropertyType);
                }

                var assignment = Expression.Assign(propertyAccess, valueExpression);
                var lambda = Expression.Lambda<StructSetter>(assignment, objParam, valueParam);

                return lambda.Compile();
            }
            catch (Exception ex)
            {
                throw new SerializationException($"Failed to create struct setter for property {property.Name}", ex);
            }
        }

        /// <summary>
        /// Throws ObjectDisposedException if disposed.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
            => ObjectDisposedException.ThrowIf(_disposed, nameof(PropertyAccessorImpl<TProp>));

        #endregion Private Methods

        #region IDisposable Implementation

        /// <summary>
        /// Releases resources used by this property accessor.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Dispose formatter if it implements IDisposable
                if (_formatter is IDisposable disposableFormatter)
                {
                    disposableFormatter.Dispose();
                }
            }
            finally
            {
                _disposed = true;
            }
        }

        #endregion IDisposable Implementation
    }

    #endregion Nested Types
}
