namespace Nalix.Common.Logging;

/// <summary>
/// Identifies a logging event. The primary identifier is the "TransportProtocol" property, with the "Name" property providing a short description of this type of event.
/// </summary>
/// <remarks>
/// Initializes an instance of the <see cref="EventId"/> struct.
/// </remarks>
/// <param name="id">The numeric identifier for this event.</param>
/// <param name="name">The name of this event.</param>
public readonly struct EventId(System.Int32 id, System.String name = null) : System.IEquatable<EventId>
{
    /// <summary>
    /// Represents an empty or uninitialized <see cref="EventId"/> with an TransportProtocol of 0.
    /// This value is commonly used to represent a default or missing event.
    /// </summary>
    public static readonly EventId Empty = new(0);

    /// <summary>
    /// Implicitly creates an EventId from the given <see cref="System.Int32"/>.
    /// </summary>
    /// <param name="i">The <see cref="System.Int32"/> to convert to an EventId.</param>
    public static implicit operator EventId(System.Int32 i) => new(i);

    /// <summary>
    /// Checks if two specified <see cref="EventId"/> instances have the same value. They are equal if they have the same TransportProtocol.
    /// </summary>
    /// <param name="left">The first <see cref="EventId"/>.</param>
    /// <param name="right">The second <see cref="EventId"/>.</param>
    /// <returns><see langword="true" /> if the objects are equal.</returns>
    public static System.Boolean operator ==(EventId left, EventId right) => left.Equals(right);

    /// <summary>
    /// Checks if two specified <see cref="EventId"/> instances have different values.
    /// </summary>
    /// <param name="left">The first <see cref="EventId"/>.</param>
    /// <param name="right">The second <see cref="EventId"/>.</param>
    /// <returns><see langword="true" /> if the objects are not equal.</returns>
    public static System.Boolean operator !=(EventId left, EventId right) => !left.Equals(right);

    /// <summary>
    /// Gets the numeric identifier for this event.
    /// </summary>
    public System.Int32 Id { get; } = id;

    /// <summary>
    /// Gets the name of this event.
    /// </summary>
    public System.String Name { get; } = name;

    /// <inheritdoc />
    public override System.String ToString() => Name ?? Id.ToString();

    /// <summary>
    /// Compares the current instance to another object of the same type. Two events are equal if they have the same TransportProtocol.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns><see langword="true" /> if the current object is equal to <paramref name="other" />; otherwise, <see langword="false" />.</returns>
    public System.Boolean Equals(EventId other) => Id == other.Id;

    /// <inheritdoc />
    public override System.Boolean Equals(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] System.Object obj) => obj is not null && obj is EventId eventId && Equals(eventId);

    /// <inheritdoc />
    public override System.Int32 GetHashCode() => Id;
}
