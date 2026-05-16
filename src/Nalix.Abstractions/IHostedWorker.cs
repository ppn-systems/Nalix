// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Concurrency;

namespace Nalix.Abstractions;

/// <summary>
/// Represents a long-running worker that is executed by the runtime.
/// </summary>
public interface IHostedWorker
{
    /// <summary>
    /// Executes the worker asynchronously.
    /// </summary>
    /// <param name="context">
    /// The execution context associated with the worker lifecycle.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to request cancellation of the worker.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the lifetime of the worker execution.
    /// </returns>
    ValueTask ExecuteAsync(
        IWorkerContext context,
        CancellationToken cancellationToken = default);
}
