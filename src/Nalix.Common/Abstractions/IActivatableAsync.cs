// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Common.Abstractions;

/// <summary>
/// Defines an asynchronous lifecycle contract for components that can be activated and deactivated.
/// </summary>
public interface IActivatableAsync : IDisposable
{
    /// <summary>
    /// Activates the component asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel activation.</param>
    /// <returns>A task that completes when activation has finished.</returns>
    Task ActivateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates the component asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel deactivation.</param>
    /// <returns>A task that completes when deactivation has finished.</returns>
    Task DeactivateAsync(CancellationToken cancellationToken = default);
}
