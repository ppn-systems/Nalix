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
        => WriteLog(LogLevel.Trace, EventId.Empty, SanitizeLogMessage(message));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Trace(string format, params object[] args)
        => Publish(LogLevel.Trace, EventId.Empty, format, args);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Trace(string message, EventId? eventId = null)
        => WriteLog(LogLevel.Trace, eventId ?? EventId.Empty, SanitizeLogMessage(message));

    #endregion Trace Methods

    #region Debug Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Debug(string message)
        => WriteLog(LogLevel.Debug, EventId.Empty, SanitizeLogMessage(message));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Debug(string format, params object[] args)
        => Publish(LogLevel.Debug, EventId.Empty, format, args);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Debug(string message, EventId? eventId = null)
        => WriteLog(LogLevel.Debug, eventId ?? EventId.Empty, SanitizeLogMessage(message));

    #endregion Debug Methods

    #region Info Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Info(string message)
        => WriteLog(LogLevel.Info, EventId.Empty, message);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Info(string format, params object[] args)
        => Publish(LogLevel.Info, EventId.Empty, format, args);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Info(string message, EventId? eventId = null)
        => WriteLog(LogLevel.Info, eventId ?? EventId.Empty, message);

    #endregion Info Methods

    #region Warn Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Warn(string message)
        => WriteLog(LogLevel.Warn, EventId.Empty, message);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Warn(string format, params object[] args)
        => Publish(LogLevel.Warn, EventId.Empty, format, args);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Warn(string message, EventId? eventId = null)
        => WriteLog(LogLevel.Warn, eventId ?? EventId.Empty, message);

    #endregion Warn Methods

    #region Error Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Error(string format, params object[] args)
        => Publish(LogLevel.Error, EventId.Empty, format, args);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Error(string message, EventId? eventId = null)
        => WriteLog(LogLevel.Error, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Error(string message, Exception exception, EventId? eventId = null)
        => WriteLog(LogLevel.Error, eventId ?? EventId.Empty, message, exception);

    #endregion Error Methods

    #region Fatal Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fatal(string format, params object[] args)
        => Publish(LogLevel.Critical, EventId.Empty, format, args);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fatal(string message, EventId? eventId = null)
        => WriteLog(LogLevel.Critical, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Fatal(string message, Exception exception, EventId? eventId = null)
        => WriteLog(LogLevel.Critical, eventId ?? EventId.Empty, message, exception);

    #endregion Fatal Methods
}
