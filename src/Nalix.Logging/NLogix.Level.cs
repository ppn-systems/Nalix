// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;

namespace Nalix.Logging;

public sealed partial class NLogix
{
    #region Meta Methods

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Meta(System.String message)
        => WriteLog(LogLevel.Meta, EventId.Empty, message);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Meta(System.String format, params System.Object[] args)
        => base.CreateFormattedLogEntry(LogLevel.Meta, EventId.Empty, format, args);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Meta(System.String message, EventId? eventId = null)
        => WriteLog(LogLevel.Meta, eventId ?? EventId.Empty, message);

    #endregion Meta Methods

    #region Trace Methods

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Trace(System.String message)
        => WriteLog(LogLevel.Trace, EventId.Empty, SanitizeLogMessage(message));

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Trace(System.String format, params System.Object[] args)
        => base.CreateFormattedLogEntry(LogLevel.Trace, EventId.Empty, format, args);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Trace(System.String message, EventId? eventId = null)
        => WriteLog(LogLevel.Trace, eventId ?? EventId.Empty, SanitizeLogMessage(message));

    #endregion Trace Methods

    #region Debug Methods

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Debug(System.String message)
        => WriteLog(LogLevel.Debug, EventId.Empty, SanitizeLogMessage(message));

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Debug(System.String format, params System.Object[] args)
        => base.CreateFormattedLogEntry(LogLevel.Debug, EventId.Empty, format, args);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Debug(System.String message, EventId? eventId = null)
        => WriteLog(LogLevel.Debug, eventId ?? EventId.Empty, SanitizeLogMessage(message));

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Debug<TClass>(
        System.String message, EventId? eventId = null,
        [System.Runtime.CompilerServices.CallerMemberName] System.String memberName = "")
        where TClass : class
        => WriteLog(LogLevel.Debug, eventId ?? EventId.Empty, $"[{typeof(TClass).Name}:{memberName}] {message}");

    #endregion Debug Methods

    #region Info Methods

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Info(System.String message)
        => WriteLog(LogLevel.Information, EventId.Empty, message);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Info(System.String format, params System.Object[] args)
        => base.CreateFormattedLogEntry(LogLevel.Information, EventId.Empty, format, args);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Info(System.String message, EventId? eventId = null)
        => WriteLog(LogLevel.Information, eventId ?? EventId.Empty, message);

    #endregion Info Methods

    #region Warn Methods

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Warn(System.String message)
        => WriteLog(LogLevel.Warning, EventId.Empty, message);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Warn(System.String format, params System.Object[] args)
        => base.CreateFormattedLogEntry(LogLevel.Warning, EventId.Empty, format, args);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Warn(System.String message, EventId? eventId = null)
        => WriteLog(LogLevel.Warning, eventId ?? EventId.Empty, message);

    #endregion Warn Methods

    #region Error Methods

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Error(System.String message)
        => WriteLog(LogLevel.Error, EventId.Empty, message);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Error(System.String format, params System.Object[] args)
        => base.CreateFormattedLogEntry(LogLevel.Error, EventId.Empty, format, args);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Error(System.Exception exception)
        => WriteLog(LogLevel.Error, EventId.Empty, exception.Message, exception);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Error(System.String message, EventId? eventId = null)
        => WriteLog(LogLevel.Error, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Error(System.Exception exception, EventId? eventId = null)
        => WriteLog(LogLevel.Error, eventId ?? EventId.Empty, exception.Message, exception);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Error(System.String message, System.Exception exception)
        => WriteLog(LogLevel.Error, EventId.Empty, message, exception);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Error(System.String message, System.Exception exception, EventId? eventId = null)
        => WriteLog(LogLevel.Error, eventId ?? EventId.Empty, message, exception);

    #endregion Error Methods

    #region Fatal Methods

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Fatal(System.String message)
        => WriteLog(LogLevel.Critical, EventId.Empty, message);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Fatal(System.String format, params System.Object[] args)
        => base.CreateFormattedLogEntry(LogLevel.Critical, EventId.Empty, format, args);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Fatal(System.String message, EventId? eventId = null)
        => WriteLog(LogLevel.Critical, eventId ?? EventId.Empty, message);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Fatal(System.String message, System.Exception exception)
        => WriteLog(LogLevel.Critical, EventId.Empty, message, exception);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Fatal(System.String message, System.Exception exception, EventId? eventId = null)
        => WriteLog(LogLevel.Critical, eventId ?? EventId.Empty, message, exception);

    #endregion Fatal Methods
}
