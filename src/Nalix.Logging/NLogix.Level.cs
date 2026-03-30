// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Nalix.Logging;

public sealed partial class NLogix
{
    #region Trace Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Trace(string message)
        => this.WriteLog(LogLevel.Trace, null, SanitizeLogMessage(message));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Trace(string message, EventId? eventId = null)
        => this.WriteLog(LogLevel.Trace, eventId ?? null, SanitizeLogMessage(message));

    #endregion Trace Methods

    #region Debug Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Debug(string message)
        => this.WriteLog(LogLevel.Debug, null, SanitizeLogMessage(message));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Debug(string message, EventId? eventId = null)
        => this.WriteLog(LogLevel.Debug, eventId ?? null, SanitizeLogMessage(message));

    #endregion Debug Methods

    #region Info Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Info(string message)
        => this.WriteLog(LogLevel.Information, null, message);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Info(string message, EventId? eventId = null)
        => this.WriteLog(LogLevel.Information, eventId ?? null, message);

    #endregion Info Methods

    #region Warn Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Warn(string message)
        => this.WriteLog(LogLevel.Warning, new EventId(), message);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Warn(string message, EventId? eventId = null)
        => this.WriteLog(LogLevel.Warning, eventId ?? null, message);

    #endregion Warn Methods

    #region Error Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Error(string message, EventId? eventId = null)
        => this.WriteLog(LogLevel.Error, eventId ?? null, message);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Error(string message, Exception exception, EventId? eventId = null)
        => this.WriteLog(LogLevel.Error, eventId ?? null, message, exception);

    #endregion Error Methods

    #region Fatal Methods

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Critical(string message, EventId? eventId = null)
        => this.WriteLog(LogLevel.Critical, eventId ?? null, message);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Critical(string message, Exception exception, EventId? eventId = null)
        => this.WriteLog(LogLevel.Critical, eventId ?? null, message, exception);

    #endregion Fatal Methods
}
