// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Processes incoming frames for a connection.
/// </summary>
public interface IFrameProcessor
{
    /// <summary>
    /// Processes a received frame.
    /// </summary>
    /// <param name="sender">
    /// The source that raised the frame event.
    /// </param>
    /// <param name="args">
    /// The connection event arguments.
    /// </param>
    void ProcessFrame(object? sender, IConnectionEventArgs args);
}
