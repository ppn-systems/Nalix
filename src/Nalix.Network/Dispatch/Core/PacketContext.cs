using Nalix.Common.Caching;
using Nalix.Common.Connection;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Dispatch.Core;

/// <summary>
/// Enhanced PacketContext với pooling support và zero-allocation design.
/// </summary>
/// <typeparam name="TPacket">Packet type</typeparam>
public sealed class PacketContext<TPacket> : System.IDisposable, IPoolable
{
    #region Fields

    private TPacket _packet = default!;
    private IConnection _connection = default!;
    private PacketMetadata _descriptor;
    private bool _isInitialized;

    // Context state
    private readonly System.Collections.Generic.Dictionary<System.String, System.Object> _properties = [];

    #endregion Fields

    #region Properties

    /// <summary>
    /// Current packet being processed.
    /// </summary>
    public TPacket Packet
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _packet;
    }

    /// <summary>
    /// Connection associated with packet.
    /// </summary>
    public IConnection Connection
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _connection;
    }

    /// <summary>
    /// Packet descriptor với attributes.
    /// </summary>
    public PacketMetadata Attributes
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _descriptor;
    }

    /// <summary>
    /// Properties dictionary để middleware có thể share data.
    /// </summary>
    public System.Collections.Generic.IDictionary<System.String, System.Object> Properties
        => _properties;

    #endregion Properties

    #region Constructor

    static PacketContext()
    {
        // Register pool for PacketContext<TPacket>
        ObjectPoolManager.Instance.Prealloc<PacketContext<TPacket>>(10);
    }

    /// <summary>
    /// Default constructor cho pooling.
    /// </summary>
    public PacketContext()
    { }

    /// <summary>
    /// Constructor với initial values.
    /// </summary>
    public PacketContext(TPacket packet, IConnection connection)
    {
        Initialize(packet, connection, default);
    }

    #endregion Constructor

    #region Methods

    /// <summary>
    /// Initialize context với new values (dùng khi rent từ pool).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal void Initialize(TPacket packet, IConnection connection, PacketMetadata descriptor)
    {
        _packet = packet;
        _connection = connection;
        _descriptor = descriptor;
        _isInitialized = true;
    }

    /// <summary>
    /// Reset context state để có thể return về pool.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal void Reset()
    {
        _packet = default!;
        _connection = default!;
        _descriptor = default;
        _isInitialized = false;
        _properties.Clear();
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal void SetPacket(TPacket packet)
    {
        _packet = packet;
    }

    /// <summary>
    /// Set property value.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetProperty<T>(System.String key, T value) where T : notnull
    {
        _properties[key] = value;
    }

    /// <summary>
    /// Get property value.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T? GetProperty<T>(System.String key) where T : class
    {
        return _properties.TryGetValue(key, out var value) ? value as T : null;
    }

    /// <summary>
    /// Get value type property.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T GetValueProperty<T>(System.String key, T defaultValue = default) where T : struct
    {
        if (_properties.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return defaultValue;
    }

    #endregion Methods

    #region IDisposable

    /// <summary>
    /// Reset context.
    /// </summary>
    public void ResetForPool()
    {
        if (_isInitialized)
        {
            this.Reset();
        }
    }

    /// <summary>
    /// Dispose context.
    /// </summary>
    public void Dispose()
    {
        if (_isInitialized)
        {
            this.Reset();
        }
    }

    #endregion IDisposable
}