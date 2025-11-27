// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Abstractions;

/// <summary>
/// Defines a standard contract for components that can be
/// activated and deactivated as part of their lifecycle.
/// </summary>
/// <remarks>
/// Implement this interface when you need to explicitly control
/// the start/stop or enable/disable state of a component.
/// Typical use cases include background services, processors,
/// or managers that must be toggled at runtime.
/// </remarks>
public interface IActivatable : System.IDisposable
{
    /// <summary>
    /// Activates the component, transitioning it into an operational state.
    /// </summary>
    /// <remarks>
    /// This method should be idempotent: calling it multiple times
    /// should not cause side effects if the component is already active.
    /// </remarks>
    void Activate([System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates the component, transitioning it into a non-operational state.
    /// </summary>
    /// <remarks>
    /// This method should release any resources or stop any
    /// background work started during <see cref="Activate"/>.
    /// It should be safe to call multiple times.
    /// </remarks>
    void Deactivate([System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken cancellationToken = default);
}
