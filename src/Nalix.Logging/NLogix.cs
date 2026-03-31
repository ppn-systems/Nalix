// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Nalix.Logging.Configuration;
using Nalix.Logging.Engine;

namespace Nalix.Logging;

/// <summary>
/// <para>
/// Provides a high-performance, extensible logging engine for applications,
/// combining structured logging and customizable output targets.
/// </para>
/// <para>
/// This class is the core of the Nalix logging system, and implements <see cref="ILogger"/> for unified logging.
/// Use this logger to write diagnostic messages, errors, warnings, or audit logs across the application.
/// </para>
/// </summary>
/// <remarks>
/// The <see cref="NLogix"/> logger supports dependency injection or can be accessed via <see cref="Host"/>.
/// Logging targets and behavior can be customized during initialization using <see cref="NLogixOptions"/>.
/// </remarks>
[DebuggerNonUserCode]
[ExcludeFromCodeCoverage]
[DebuggerDisplay("Logger=NLogix, {GetType().Name,nq}")]
public sealed partial class NLogix : NLogixEngine, ILogger
{
    #region Constructors

    /// <summary>
    /// Initializes the logging system with optional configuration.
    /// </summary>
    /// <param name="configure">An optional action to configure the logging system.</param>
    [SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public NLogix(Action<NLogixOptions>? configure = null)
        : base(configure)
    {
    }

    #endregion Constructors
}
