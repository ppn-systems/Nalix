// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Networking;

/// <summary>
/// Represents a single stage (step) in a protocol pipeline.
/// Each stage can inspect, modify, or reject a frame, and may terminate pipeline execution
/// by throwing an exception upon protocol or business errors.
/// </summary>
public interface IProtocolStage
{
    /// <summary>
    /// Handles a protocol message at this pipeline stage.
    /// </summary>
    /// <param name="sender">
    /// The source of the event (typically the connection or pipeline).
    /// </param>
    /// <param name="args">
    /// The protocol event arguments containing the network frame and context information.
    /// Must not be <c>null</c>.
    /// </param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if <paramref name="args"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.InvalidCastException">
    /// Thrown if <paramref name="args"/> is of an incorrect or unsupported type for this stage.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if the frame is missing required information or protocol invariants are violated.
    /// </exception>
    void ProcessMessage(object? sender, IConnectEventArgs args);
}
