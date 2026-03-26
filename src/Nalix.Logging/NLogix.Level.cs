// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using Nalix.Common.Diagnostics;

namespace Nalix.Logging;

public sealed partial class NLogix
{
    #region Trace Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Trace(string message)
        => this.WriteLog(LogLevel.Trace, EventId.Empty, SanitizeLogMessage(message));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Trace(string format, params object[] args)
        => this.Publish(LogLevel.Trace, EventId.Empty, format, args);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Trace(string message, EventId? eventId = null)
        => this.WriteLog(LogLevel.Trace, eventId ?? EventId.Empty, SanitizeLogMessage(message));

    #endregion Trace Methods

    #region Debug Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Debug(string message)
        => this.WriteLog(LogLevel.Debug, EventId.Empty, SanitizeLogMessage(message));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Debug(string format, params object[] args)
        => this.Publish(LogLevel.Debug, EventId.Empty, format, args);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Debug(string message, EventId? eventId = null)
        => this.WriteLog(LogLevel.Debug, eventId ?? EventId.Empty, SanitizeLogMessage(message));

    #endregion Debug Methods

    #region Info Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Info(string message)
        => this.WriteLog(LogLevel.Info, EventId.Empty, message);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Info(string format, params object[] args)
        => this.Publish(LogLevel.Info, EventId.Empty, format, args);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Info(string message, EventId? eventId = null)
        => this.WriteLog(LogLevel.Info, eventId ?? EventId.Empty, message);

    #endregion Info Methods

    #region Warn Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Warn(string message)
        => this.WriteLog(LogLevel.Warn, EventId.Empty, message);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Warn(string format, params object[] args)
        => this.Publish(LogLevel.Warn, EventId.Empty, format, args);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Warn(string message, EventId? eventId = null)
        => this.WriteLog(LogLevel.Warn, eventId ?? EventId.Empty, message);

    #endregion Warn Methods

    #region Error Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Error(string format, params object[] args)
        => this.Publish(LogLevel.Error, EventId.Empty, format, args);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Error(string message, EventId? eventId = null)
        => this.WriteLog(LogLevel.Error, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Error(string message, Exception exception, EventId? eventId = null)
        => this.WriteLog(LogLevel.Error, eventId ?? EventId.Empty, message, exception);

    #endregion Error Methods

    #region Fatal Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fatal(string format, params object[] args)
        => this.Publish(LogLevel.Critical, EventId.Empty, format, args);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fatal(string message, EventId? eventId = null)
        => this.WriteLog(LogLevel.Critical, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Fatal(string message, Exception exception, EventId? eventId = null)
        => this.WriteLog(LogLevel.Critical, eventId ?? EventId.Empty, message, exception);

    #endregion Fatal Methods
}
