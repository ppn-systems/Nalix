// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;

namespace Nalix.Abstractions;

/// <summary>
/// Defines a contract for adding logging capability to a component.
/// </summary>
/// <typeparam name="T">
/// The type of the object that implements this interface, used to support fluent chaining.
/// </typeparam>
public interface IWithLogging<T>
{
    /// <summary>
    /// Configures the current instance to use the specified <see cref="ILogger"/> for logging.
    /// </summary>
    /// <param name="logger">
    /// The logger instance used to write log messages.
    /// </param>
    /// <returns>
    /// The current instance of type <typeparamref name="T"/> to allow method chaining.
    /// </returns>
    T WithLogging(ILogger logger);
}
