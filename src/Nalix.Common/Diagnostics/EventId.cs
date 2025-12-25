// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Nalix.Common.Diagnostics;

/// <summary>
/// Identifies a logging event. The primary identifier is the "ProtocolType" property, with the "Name" property providing a short description of this type of event.
/// </summary>
/// <remarks>
/// Initializes an instance of the <see cref="EventId"/> struct.
/// </remarks>
/// <param name="id">The numeric identifier for this event.</param>
/// <param name="name">The name of this event.</param>
public readonly struct EventId(int id, string? name = null) : IEquatable<EventId>
{
    /// <summary>
    /// Represents an empty or uninitialized <see cref="EventId"/> with an ProtocolType of 0.
    /// This value is commonly used to represent a default or missing event.
    /// </summary>
    public static readonly EventId Empty = new(0);

    /// <summary>
    /// Implicitly creates an EventId from the given <see cref="int"/>.
    /// </summary>
    /// <param name="i">The <see cref="int"/> to convert to an EventId.</param>
    public static implicit operator EventId(int i) => new(i);

    /// <summary>
    /// Checks if two specified <see cref="EventId"/> instances have the same value. They are equal if they have the same ProtocolType.
    /// </summary>
    /// <param name="left">The first <see cref="EventId"/>.</param>
    /// <param name="right">The second <see cref="EventId"/>.</param>
    /// <returns><see langword="true" /> if the objects are equal.</returns>
    public static bool operator ==(EventId left, EventId right) => left.Equals(right);

    /// <summary>
    /// Checks if two specified <see cref="EventId"/> instances have different values.
    /// </summary>
    /// <param name="left">The first <see cref="EventId"/>.</param>
    /// <param name="right">The second <see cref="EventId"/>.</param>
    /// <returns><see langword="true" /> if the objects are not equal.</returns>
    public static bool operator !=(EventId left, EventId right) => !left.Equals(right);

    /// <summary>
    /// Gets the numeric identifier for this event.
    /// </summary>
    public int Id { get; } = id;

    /// <summary>
    /// Gets the name of this event.
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <inheritdoc />
    public override string ToString() => Name ?? Id.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Compares the current instance to another object of the same type. Two events are equal if they have the same ProtocolType.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns><see langword="true" /> if the current object is equal to <paramref name="other" />; otherwise, <see langword="false" />.</returns>
    public bool Equals([NotNull] EventId other) => Id == other.Id;

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is not null && obj is EventId eventId && Equals(eventId);

    /// <inheritdoc />
    public override int GetHashCode() => Id;
}
