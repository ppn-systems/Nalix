using System;

namespace Notio.Common.Models;

/// <summary>
/// Represents a log event. The identifier is the "Id" property, with the "Name" property providing a brief description of the event type.
/// </summary>
/// <remarks>
/// Initializes an instance of the <see cref="EventId"/> struct.
/// </remarks>
/// <param name="id">The numeric identifier of this event.</param>
/// <param name="name">The name of this event.</param>
public readonly struct EventId(int id, string name = null) : IEquatable<EventId>
{
    /// <summary>
    /// Represents an empty or uninitialized <see cref="EventId"/> with an ID of 0.
    /// This value is commonly used to represent a default or missing event.
    /// </summary>
    public static readonly EventId Empty = new(0);

    /// <summary>
    /// Implicitly creates an EventId from the provided <see cref="int"/>.
    /// </summary>
    /// <param name="i">The <see cref="int"/> value to convert into an EventId.</param>
    public static implicit operator EventId(int i) => new(i);

    /// <summary>
    /// Checks if two specified <see cref="EventId"/> instances are equal. They are considered equal if they have the same ID.
    /// </summary>
    /// <param name="left">The first <see cref="EventId"/> instance.</param>
    /// <param name="right">The second <see cref="EventId"/> instance.</param>
    /// <returns><see langword="true" /> if the instances are equal.</returns>
    public static bool operator ==(EventId left, EventId right) => left.Equals(right);

    /// <summary>
    /// Checks if two specified <see cref="EventId"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first <see cref="EventId"/> instance.</param>
    /// <param name="right">The second <see cref="EventId"/> instance.</param>
    /// <returns><see langword="true" /> if the instances are not equal.</returns>
    public static bool operator !=(EventId left, EventId right) => !left.Equals(right);

    /// <summary>
    /// Gets the numeric identifier of this event.
    /// </summary>
    public int Id { get; } = id;

    /// <summary>
    /// Gets the name of this event.
    /// </summary>
    public string Name { get; } = name;

    /// <inheritdoc />
    public override string ToString() => Name ?? Id.ToString();

    /// <summary>
    /// Compares the current instance with another object of the same type. Two events are considered equal if they have the same ID.
    /// </summary>
    /// <param name="other">An object to compare with this instance.</param>
    /// <returns><see langword="true" /> if the current object is equal to <paramref name="other"/>; otherwise, <see langword="false" />.</returns>
    public bool Equals(EventId other) => Id == other.Id;

    /// <inheritdoc />
    public override bool Equals(object obj)
    {
        if (obj is null)
        {
            return false;
        }

        return obj is EventId eventId && Equals(eventId);
    }

    /// <inheritdoc />
    public override int GetHashCode() => Id;
}
