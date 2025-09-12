// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Abstractions;

/// <summary>
/// Defines a standard contract for components that can be
/// asynchronously activated and deactivated as part of their lifecycle.
/// </summary>
/// <remarks>
/// Implement this interface when the activation or deactivation
/// process involves asynchronous operations such as I/O, networking,
/// or long-running initialization/cleanup tasks.
/// </remarks>
public interface IAsyncActivatable : System.IAsyncDisposable
{
    /// <summary>
    /// Asynchronously activates the component, transitioning it into an operational state.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous activation operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous activation operation.
    /// </returns>
    System.Threading.Tasks.Task ActivateAsync(System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deactivates the component, transitioning it into a non-operational state.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous deactivation operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous deactivation operation.
    /// </returns>
    System.Threading.Tasks.Task DeactivateAsync(System.Threading.CancellationToken cancellationToken = default);
}
