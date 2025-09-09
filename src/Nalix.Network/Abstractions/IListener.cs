// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Framework.Time;

namespace Nalix.Network.Abstractions;

/// <summary>
/// Interface for network listener classes.
/// This interface is intended to be implemented by classes that listen for network connections
/// and handle the initiation and termination of connection listening.
/// </summary>
public interface IListener : IActivatable
{
    /// <summary>
    /// Updates the listener with the current server time, provided as a Unix timestamp.
    /// </summary>
    /// <param name="milliseconds">The current server time in milliseconds since the Unix epoch (January 1, 2020, 00:00:00 UTC), as provided by <see cref="Clock.UnixMillisecondsNow"/>.</param>
    void SynchronizeTime(System.Int64 milliseconds);
}
