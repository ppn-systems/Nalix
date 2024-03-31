// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;

namespace Nalix.Common.Tasks;

/// <summary>
/// Provides context and control for a long-running worker task, including identification, group information,
/// heartbeat signaling, progress reporting, and cancellation status.
/// </summary>
public interface IWorkerContext
{
    /// <summary>
    /// Gets the unique identifier of the worker.
    /// </summary>
    IIdentifier Id { get; }

    /// <summary>
    /// Gets the name of the worker.
    /// </summary>
    System.String Name { get; }

    /// <summary>
    /// Gets the group to which the worker belongs.
    /// </summary>
    System.String Group { get; }

    /// <summary>
    /// Gets a value indicating whether cancellation has been requested for the worker.
    /// </summary>
    System.Boolean IsCancellationRequested { get; }

    /// <summary>
    /// Sends a heartbeat signal to indicate the worker is still active.
    /// </summary>
    void Beat();

    /// <summary>
    /// Adds progress to the worker, with an optional note describing the update.
    /// </summary>
    /// <param name="delta">The amount of progress to add.</param>
    /// <param name="note">An optional note describing the progress update.</param>
    void Advance(System.Int64 delta, System.String note = null);
}