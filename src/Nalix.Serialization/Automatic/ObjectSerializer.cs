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
/// Provides an optimized automatic serializer for objects, eliminating boxing
/// and achieving near hand-written serialization performance.
/// Implements SOLID principles and follows Domain-Driven Design patterns.
/// </summary>
/// <typeparam name="T">The type of object to serialize.</typeparam>
public sealed class ObjectSerializer<T> : IFormatter<T>, IDisposable where T : class, new()
{
    #region Fields and Properties

    /// <summary>
    /// Cached array of property accessors for efficient serialization.
    /// </summary>
    private readonly PropertyAccessor[] _accessors;

    /// <summary>
    /// Configuration options for serialization behavior.
    /// </summary>
    private readonly SerializationOptions _options;

    /// <summary>
    /// Logger for diagnostic and error tracking.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Activity source for telemetry and performance monitoring.
    /// </summary>
    private static readonly ActivitySource ActivitySource = new($"Nalix.Serialization.{typeof(T).Name}");

    /// <summary>
    /// Indicates whether this instance has been disposed.
    /// </summary>
    private bool _disposed;

    #endregion Fields and Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="ObjectSerializer{T}"/> with default options.
    /// </summary>
    public ObjectSerializer() : this(SerializationOptions.Default, null) { }

    /// <summary>
    /// Initializes a new instance of <see cref="ObjectSerializer{T}"/> with custom options.
    /// </summary>
    /// <param name="options">Serialization configuration options.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public ObjectSerializer(SerializationOptions options, ILogger logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        try
        {
            _accessors = CreatePropertyAccessors();
            _logger?.Info("ObjectSerializer<{0}> initialized with {1} properties", typeof(T).Name, _accessors.Length);
        }
        catch (Exception ex)
        {
            _logger?.Error($"Failed to initialize ObjectSerializer<{typeof(T).Name}>", ex);
            throw new SerializationException($"Failed to initialize formatter for type {typeof(T).Name}", ex);
        }
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Serializes the given object into the provided binary writer with comprehensive error handling.
    /// </summary>
    /// <param name="writer">The binary writer used for serialization.</param>
    /// <param name="value">The object to serialize.</param>
    /// <exception cref="ArgumentNullException">Thrown when writer or value is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when formatter has been disposed.</exception>
    /// <exception cref="SerializationException">Thrown when serialization fails.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T value)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        using var activity = ActivitySource.StartActivity($"Serialize.{typeof(T).Name}");

        try
        {
            // Serialize all properties
            for (int i = 0; i < _accessors.Length; i++)
            {
                _accessors[i].Serialize(ref writer, value);
            }

            _logger?.Debug("Successfully serialized object of type {0}", typeof(T).Name);
        }
        catch (Exception ex) when (ex is not SerializationException)
        {
            var serializationEx = new SerializationException($"Failed to serialize object of type {typeof(T).Name}", ex);
            _logger?.Error("Serialization failed for type {0}:{1}", typeof(T).Name, serializationEx);
            throw serializationEx;
        }
    }

    /// <summary>
    /// Deserializes an object from the provided binary reader with comprehensive validation.
    /// </summary>
    /// <param name="reader">The binary reader containing serialized data.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when reader is null.</exception>
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
            T obj = CreateInstance();

            // Deserialize all properties
            for (int i = 0; i < _accessors.Length; i++)
            {
                _accessors[i].Deserialize(ref reader, obj);
            }

            _logger?.Debug("Successfully deserialized object of type {0}", typeof(T).Name);
            return obj;
        }
        catch (Exception ex) when (ex is not SerializationException)
        {
            var serializationEx = new SerializationException($"Failed to deserialize object of type {typeof(T).Name}", ex);
            _logger?.Error("Deserialization failed for type {0}:{1}", typeof(T).Name, serializationEx);
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

                _logger?.Warn("Skipping property {0} due to error {1}", property.Name, ex);
            }
        }

        return [.. accessors];
    }

    /// <summary>
    /// Creates an instance of T using the most appropriate constructor.
    /// Implements Dependency Inversion Principle support.
    /// </summary>
    /// <returns>New instance of T.</returns>
    private static T CreateInstance()
    {
        try
        {
            return Activator.CreateInstance<T>();
        }
        catch (Exception ex)
        {
            throw new SerializationException(
                $"Cannot create instance of type {typeof(T).Name}. Ensure it has a parameterless constructor.", ex);
        }
    }

    /// <summary>
    /// Throws ObjectDisposedException if this instance has been disposed.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, nameof(ObjectSerializer<T>));

    #endregion Private Methods

    #region IDisposable Implementation

    /// <summary>
    /// Releases all resources used by the ObjectSerializer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        ActivitySource.Dispose();
        _disposed = true;

        _logger?.Debug("ObjectSerializer<{0}> disposed", typeof(T).Name);
    }

    #endregion IDisposable Implementation

    #region Nested Types

    /// <summary>
    /// Abstract base class for property serialization following the Strategy Pattern.
    /// Implements the Open/Closed Principle for extensibility.
    /// </summary>
    private abstract class PropertyAccessor
    {
        /// <summary>
        /// Gets the name of the property this accessor handles.
        /// </summary>
        public abstract string PropertyName { get; }

        /// <summary>
        /// Serializes a property of the given object.
        /// </summary>
        /// <param name="writer">The binary writer used for serialization.</param>
        /// <param name="obj">The object containing the property.</param>
        public abstract void Serialize(ref DataWriter writer, T obj);

        /// <summary>
        /// Deserializes a property into the given object.
        /// </summary>
        /// <param name="reader">The binary reader containing serialized data.</param>
        /// <param name="obj">The object to populate with deserialized data.</param>
        public abstract void Deserialize(ref DataReader reader, T obj);

        /// <summary>
        /// Factory method that creates a strongly typed property accessor.
        /// Implements the Factory Pattern for type-safe creation.
        /// </summary>
        /// <param name="property">The property to generate an accessor for.</param>
        /// <param name="options">Serialization options.</param>
        /// <returns>A specialized property accessor.</returns>
        /// <exception cref="ArgumentNullException">Thrown when property or options is null.</exception>
        public static PropertyAccessor Create(PropertyInfo property, SerializationOptions options)
        {
            ArgumentNullException.ThrowIfNull(property, nameof(property));
            ArgumentNullException.ThrowIfNull(options, nameof(options));

            var accessorType = typeof(PropertyAccessorImpl<,>)
                .MakeGenericType(typeof(T), property.PropertyType);

            return (PropertyAccessor)Activator.CreateInstance(accessorType, property, options)!;
        }
    }

    /// <summary>
    /// Strongly-typed property accessor implementation that eliminates boxing.
    /// Follows the Interface Segregation Principle with focused responsibilities.
    /// </summary>
    /// <typeparam name="TObj">The object type containing the property.</typeparam>
    /// <typeparam name="TProp">The property type.</typeparam>
    private sealed class PropertyAccessorImpl<TObj, TProp> : PropertyAccessor, IDisposable
        where TObj : class
    {
        #region Fields

        private readonly string _propertyName;
        private readonly Func<TObj, TProp> _getter;
        private readonly Action<TObj, TProp> _setter;
        private readonly IFormatter<TProp> _formatter;
        private readonly SerializationOptions _options;
        private bool _disposed;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the name of the property this accessor handles.
        /// </summary>
        public override string PropertyName => _propertyName;

        #endregion Properties

        #region Constructor

        /// <summary>
        /// Initializes a new property accessor with compile-time optimizations.
        /// </summary>
        /// <param name="property">The property information.</param>
        /// <param name="options">Serialization options.</param>
        public PropertyAccessorImpl(PropertyInfo property, SerializationOptions options)
        {
            ArgumentNullException.ThrowIfNull(property, nameof(property));
            ArgumentNullException.ThrowIfNull(options, nameof(options));

            _propertyName = property.Name;
            _options = options;

            try
            {
                _getter = CreateOptimizedGetter(property);
                _setter = CreateOptimizedSetter(property);
                _formatter = FormatterProvider.Get<TProp>();
            }
            catch (Exception ex)
            {
                throw new SerializationException($"Failed to create accessor for property {property.Name}", ex);
            }
        }

        #endregion Constructor

        #region Public Methods

        /// <summary>
        /// Serializes a property using type-safe, optimized access.
        /// </summary>
        /// <param name="writer">The binary writer used for serialization.</param>
        /// <param name="obj">The object containing the property.</param>
        /// <exception cref="ArgumentNullException">Thrown when obj is null.</exception>
        /// <exception cref="InvalidCastException">Thrown when obj is not of expected type.</exception>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public override void Serialize(ref DataWriter writer, T obj)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(obj, nameof(obj));

            if (obj is not TObj typedObj)
            {
                throw new InvalidCastException($"Expected object of type {typeof(TObj).Name}, but got {obj.GetType().Name}");
            }

            try
            {
                var value = _getter(typedObj);
                _formatter.Serialize(ref writer, value);
            }
            catch (Exception ex)
            {
                throw new SerializationException($"Failed to serialize property {_propertyName}", ex);
            }
        }

        /// <summary>
        /// Deserializes a property value using type-safe, optimized access.
        /// </summary>
        /// <param name="reader">The binary reader containing serialized data.</param>
        /// <param name="obj">The object to populate with deserialized data.</param>
        /// <exception cref="ArgumentNullException">Thrown when obj is null.</exception>
        /// <exception cref="InvalidCastException">Thrown when obj is not of expected type.</exception>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public override void Deserialize(ref DataReader reader, T obj)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(obj, nameof(obj));

            if (obj is not TObj typedObj)
            {
                throw new InvalidCastException($"Expected object of type {typeof(TObj).Name}, but got {obj.GetType().Name}");
            }

            try
            {
                var value = _formatter.Deserialize(ref reader);
                _setter(typedObj, value);
            }
            catch (Exception ex)
            {
                throw new SerializationException($"Failed to deserialize property {_propertyName}", ex);
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Creates an optimized getter using expression trees with comprehensive validation.
        /// </summary>
        /// <param name="property">The property to create a getter for.</param>
        /// <returns>Compiled getter function.</returns>
        private static Func<TObj, TProp> CreateOptimizedGetter(PropertyInfo property)
        {
            if (!property.CanRead)
                throw new ArgumentException($"Property {property.Name} is not readable", nameof(property));

            try
            {
                var param = Expression.Parameter(typeof(TObj), "obj");
                var propertyAccess = Expression.Property(
                    Expression.Convert(param, property.DeclaringType!),
                    property
                );

                var lambda = Expression.Lambda<Func<TObj, TProp>>(propertyAccess, param);
                return lambda.Compile();
            }
            catch (Exception ex)
            {
                throw new SerializationException($"Failed to create getter for property {property.Name}", ex);
            }
        }

        /// <summary>
        /// Creates an optimized setter using expression trees with comprehensive validation.
        /// </summary>
        /// <param name="property">The property to create a setter for.</param>
        /// <returns>Compiled setter action.</returns>
        private static Action<TObj, TProp> CreateOptimizedSetter(PropertyInfo property)
        {
            if (!property.CanWrite)
                throw new ArgumentException($"Property {property.Name} is not writable", nameof(property));

            try
            {
                var objParam = Expression.Parameter(typeof(TObj), "obj");
                var valueParam = Expression.Parameter(typeof(TProp), "value");

                var propertyAccess = Expression.Property(
                    Expression.Convert(objParam, property.DeclaringType!),
                    property
                );

                var assignment = Expression.Assign(propertyAccess, valueParam);
                var lambda = Expression.Lambda<Action<TObj, TProp>>(assignment, objParam, valueParam);

                return lambda.Compile();
            }
            catch (Exception ex)
            {
                throw new SerializationException($"Failed to create setter for property {property.Name}", ex);
            }
        }

        /// <summary>
        /// Throws ObjectDisposedException if disposed.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
            => ObjectDisposedException.ThrowIf(_disposed, nameof(PropertyAccessorImpl<TObj, TProp>));

        #endregion Private Methods

        #region IDisposable Implementation

        /// <summary>
        /// Releases resources used by this property accessor.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
        }

        #endregion IDisposable Implementation
    }

    #endregion Nested Types
}
