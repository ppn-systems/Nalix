// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Nalix.Environment.Configuration;
using Nalix.Logging.Options;
using Nalix.Logging.Sinks;

namespace Nalix.Logging;

/// <summary>
/// Default implementation of <see cref="INLogixBuilder"/> that accumulates
/// configuration and produces an <see cref="NLogix"/> instance.
/// </summary>
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
public sealed class NLogixBuilder : INLogixBuilder
{
    #region Fields

    private readonly List<INLogixTarget> _targets = [];
    private readonly List<Action<NLogixOptions>> _optionsConfigurators = [];
    private readonly List<Action<FileLogOptions>> _fileOptionsConfigurators = [];
    private LogLevel? _minLevel;

    #endregion Fields

    #region INLogixBuilder

    /// <inheritdoc/>
    public INLogixBuilder ConfigureOptions(Action<NLogixOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _optionsConfigurators.Add(configure);
        return this;
    }

    /// <inheritdoc/>
    public INLogixBuilder SetMinimumLevel(LogLevel level)
    {
        _minLevel = level;
        return this;
    }

    /// <inheritdoc/>
    public INLogixBuilder ConfigureFileOptions(Action<FileLogOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _fileOptionsConfigurators.Add(configure);
        return this;
    }

    /// <inheritdoc/>
    public INLogixBuilder AddTarget<TTarget>() where TTarget : class, INLogixTarget, new()
    {
        _targets.Add(new TTarget());
        return this;
    }

    /// <inheritdoc/>
    public INLogixBuilder AddTarget(INLogixTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _targets.Add(target);
        return this;
    }

    /// <inheritdoc/>
    public NLogix Build()
    {
        // Load options from ConfigurationManager (INI-backed)
        NLogixOptions options = ConfigurationManager.Instance.Get<NLogixOptions>();

        // Apply user-specified options configurators
        for (int i = 0; i < _optionsConfigurators.Count; i++)
        {
            _optionsConfigurators[i](options);
        }

        // Apply user-specified file options configurators
        for (int i = 0; i < _fileOptionsConfigurators.Count; i++)
        {
            _fileOptionsConfigurators[i](options.FileOptions);
        }

        // Override min level if explicitly set via SetMinimumLevel()
        if (_minLevel.HasValue)
        {
            options.MinLevel = _minLevel.Value;
        }

        // If no targets were registered, add sensible defaults
        if (_targets.Count == 0)
        {
            _targets.Add(new BatchConsoleLogTarget());
            _targets.Add(new BatchFileLogTarget());
        }

        return new NLogix([.. _targets], options);
    }

    #endregion INLogixBuilder
}
