// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Microsoft.Extensions.Logging;
using Nalix.Logging.Options;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Nalix.Logging;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Fluent builder for configuring and constructing an <see cref="NLogix"/> logger instance.
/// </summary>
public interface INLogixBuilder
{
    /// <summary>
    /// Configures the <see cref="NLogixOptions"/> using the specified delegate.
    /// </summary>
    /// <param name="configure">The callback used to configure options.</param>
    /// <returns>The current builder instance.</returns>
    INLogixBuilder ConfigureOptions(Action<NLogixOptions> configure);

    /// <summary>
    /// Sets the minimum logging level.
    /// </summary>
    /// <param name="level">The minimum log level.</param>
    /// <returns>The current builder instance.</returns>
    INLogixBuilder SetMinimumLevel(LogLevel level);

    /// <summary>
    /// Configures the <see cref="FileLogOptions"/> using the specified delegate.
    /// </summary>
    /// <param name="configure">The callback used to configure file options.</param>
    /// <returns>The current builder instance.</returns>
    INLogixBuilder ConfigureFileOptions(Action<FileLogOptions> configure);

    /// <summary>
    /// Registers a logging target type. An instance will be created using the default parameterless constructor.
    /// </summary>
    /// <typeparam name="TTarget">The target type to register.</typeparam>
    /// <returns>The current builder instance.</returns>
    INLogixBuilder AddTarget<TTarget>() where TTarget : class, INLogixTarget, new();

    /// <summary>
    /// Registers an existing logging target instance.
    /// </summary>
    /// <param name="target">The target instance to register.</param>
    /// <returns>The current builder instance.</returns>
    INLogixBuilder AddTarget(INLogixTarget target);

    /// <summary>
    /// Builds and returns a configured <see cref="NLogix"/> instance.
    /// </summary>
    /// <returns>The configured <see cref="NLogix"/> logger.</returns>
    NLogix Build();
}
