// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Shared;

namespace Nalix.Common.Networking;

/// <summary>
/// Interface for network listener classes.
/// This interface is intended to be implemented by classes that listen for network connections
/// and handle the initiation and termination of connection listening.
/// </summary>
public interface IListener : IActivatable, IReportable
{
    /// <summary>
    /// Updates the listener with the current server time, provided as a Unix timestamp.
    /// </summary>
    /// <param name="milliseconds">The current server time in milliseconds since the Unix epoch (January 1, 1970, 00:00:00 UTC), as provided by Clock.UnixMillisecondsNow/>.</param>
    void SynchronizeTime([NotNull] long milliseconds);
}
