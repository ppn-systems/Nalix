// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Environment;
using Nalix.Logging.Internal.Exceptions;

namespace Nalix.Logging.Internal;

/// <summary>
/// Manages writing logs to a file with support for daily+index rotation,
/// multi-process safe sharing, and non-throwing IO behavior.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("File={_currentPath,nq}, Size={_writtenBytesForCurrentFile}")]
internal sealed class FileWriter : System.IDisposable
{
    #region Fields

    // Larger buffer to reduce syscalls during batched writes
    private const System.Int32 WriteBufferSize = 10 * 1024;
    private const System.String DatePartFormat = "yy_MM_dd"; // => 25_09_04

    private readonly FileLoggerProvider _provider;
    private readonly System.Threading.Lock _fileLock = new();

    private System.Boolean _isDisposed;

    // New rolling state
    private System.DateTime _currentDayLocal = System.DateTime.MinValue;
    private System.Int32 _currentIndex = 0;
    private System.Int64 _writtenBytesForCurrentFile = 0;

    private System.IO.FileStream? _logFileStream;
    private System.IO.StreamWriter? _logFileWriter;
    private System.String? _currentPath;

    // Cached encoder for accurate byte accounting
    private static readonly System.Text.UTF8Encoding s_utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    #endregion Fields

    #region Constructor

    internal FileWriter(FileLoggerProvider provider)
    {
        _provider = provider ?? throw new System.ArgumentNullException(nameof(provider));

        // Initialize day/index & open first file (no-throw)
        lock (_fileLock)
        {
            _currentDayLocal = System.DateTime.Now.Date;
            _currentIndex = 0;            // CreateOrAdvanceStream() will start at 1
            _writtenBytesForCurrentFile = 0;
            CreateOrAdvanceStream();
        }
    }

    #endregion Constructor

    #region APIs

    /// <summary>
    /// Use a new log file, typically after an error with the current one.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void UseNewLogFile(System.String newLogFileName)
    {
        if (System.String.IsNullOrEmpty(newLogFileName))
        {
            // Do not throw; just ignore as per non-fatal logging policy
            return;
        }

        lock (_fileLock)
        {
            // Respect explicit request but still follow day+index scheme from now on
            Close_NoThrow_NoLock();
            _provider.Options.LogFileName = System.IO.Path.GetFileName(newLogFileName);
            // Reset rolling state to start probing from _1 for current day
            _currentDayLocal = System.DateTime.Now.Date;
            _currentIndex = 0;
            _writtenBytesForCurrentFile = 0;
            CreateOrAdvanceStream();
        }
    }

    /// <summary>
    /// Writes a message to the log file.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void WriteMessage(System.String message, System.Boolean flush)
    {
        if (System.String.IsNullOrEmpty(message))
        {
            return;
        }

        lock (_fileLock)
        {
            try
            {
                EnsureStreamReady_NoLock();

                if (_logFileWriter is null || _logFileStream is null)
                {
                    return; // drop silently
                }

                // Accurate byte size in UTF-8 (message + newline)
                System.Int32 bytes = s_utf8NoBom.GetByteCount(message) + s_utf8NoBom.GetByteCount(System.Environment.NewLine);

                // roll if will exceed limit
                if (_writtenBytesForCurrentFile + bytes > _provider.MaxFileSize)
                {
                    SafeClose_NoLock();
                    _currentIndex++;
                    CreateOrAdvanceStream();

                    if (_logFileWriter is null || _logFileStream is null)
                    {
                        return;
                    }
                }

                _logFileWriter.WriteLine(message);
                _writtenBytesForCurrentFile += bytes;

                if (flush)
                {
                    _logFileWriter.Flush();
                }
            }
            catch (System.Exception ex)
            {
                _provider.HandleFileError?.Invoke(new FileError(ex, _currentPath ?? "<unknown>"));
                try { SafeClose_NoLock(); } catch { /* ignore */ }
            }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void Flush()
    {
        lock (_fileLock)
        {
            try { _logFileWriter?.Flush(); } catch { /* ignore */ }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void Close()
    {
        lock (_fileLock)
        {
            Close_NoThrow_NoLock();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Close();
    }

    #endregion APIs

    #region Private Methods

    /// <summary>
    /// Ensure stream is open for the correct day/size; non-throwing.
    /// </summary>
    private void EnsureStreamReady_NoLock()
    {
        var now = System.DateTime.Now;
        var day = now.Date;

        if (day != _currentDayLocal)
        {
            // New day: reset index, close, reopen at _1
            SafeClose_NoLock();
            _currentDayLocal = day;
            _currentIndex = 0;
            _writtenBytesForCurrentFile = 0;
            CreateOrAdvanceStream();
            return;
        }

        if (_logFileWriter is null || _logFileStream is null)
        {
            CreateOrAdvanceStream();
            return;
        }

        if (_writtenBytesForCurrentFile >= _provider.MaxFileSize)
        {
            SafeClose_NoLock();
            _currentIndex++;
            CreateOrAdvanceStream();
        }
    }

    /// <summary>
    /// Try to open (or advance) a daily-indexed file, probing indices.
    /// Never throws; on failure, reports and leaves writer null.
    /// </summary>
    private void CreateOrAdvanceStream()
    {
        try
        {
            _ = System.IO.Directory.CreateDirectory(Directories.LogsDirectory);
        }
        catch (System.Exception ex)
        {
            _provider.HandleFileError?.Invoke(new FileError(ex, Directories.LogsDirectory));
            SafeClose_NoLock();
            return;
        }

        const System.Int32 MaxProbe = 10000;

        for (System.Int32 probe = 0; probe < MaxProbe; probe++)
        {
            if (_currentIndex <= 0)
            {
                _currentIndex = 1;
            }

            // Build file name per day+index, based on base name from Options.LogFileName
            var (noext, ext) = GetBaseNameParts(_provider.Options.LogFileName);
            var fileName = BuildDailyIndexedName(noext, ext, _currentDayLocal, _currentIndex);
            var fullPath = System.IO.Path.Combine(Directories.LogsDirectory, fileName);

            try
            {
                var info = new System.IO.FileInfo(fullPath);
                if (info.Exists && info.Length >= _provider.MaxFileSize)
                {
                    _currentIndex++;
                    continue;
                }

                _logFileStream = new System.IO.FileStream(
                    fullPath,
                    System.IO.FileMode.Append,
                    System.IO.FileAccess.Write,
                    // Cooperative sharing for multi-process and external tools
                    System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete,
                    WriteBufferSize,
                    System.IO.FileOptions.WriteThrough);

                _logFileWriter = new System.IO.StreamWriter(_logFileStream, s_utf8NoBom, WriteBufferSize)
                {
                    AutoFlush = false
                };

                _currentPath = fullPath;
                _writtenBytesForCurrentFile = info.Exists ? info.Length : 0;

                if (!info.Exists || info.Length == 0)
                {
                    WriteHeader_NoLock();
                }

                // Keep Options.LogFileName pointing to the base pattern (not the full index),
                // so external callers don’t get confused. We only use pattern for naming.
                return;
            }
            catch (System.Exception ex)
            {
                _provider.HandleFileError?.Invoke(new FileError(ex, fullPath));
                SafeClose_NoLock();
                _currentIndex++;
                continue;
            }
        }

        // Give up until next batch
        _provider.HandleFileError?.Invoke(new FileError(
            new System.IO.IOException("Exceeded max probes while selecting log file index."),
            Directories.LogsDirectory));
        SafeClose_NoLock();
    }

    /// <summary>
    /// Create header for a new file; best-effort (non-throwing).
    /// </summary>
    private void WriteHeader_NoLock()
    {
        try
        {
            if (_logFileWriter is null)
            {
                return;
            }

            var sb = new System.Text.StringBuilder(256);
            _ = sb.AppendLine("-----------------------------------------------------");
            _ = sb.AppendLine($"Log File Created: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            _ = sb.AppendLine($"User: {System.Environment.UserName}");
            _ = sb.AppendLine($"Machine: {System.Environment.MachineName}");
            _ = sb.AppendLine($"OS: {System.Environment.OSVersion}");
            _ = sb.AppendLine("-----------------------------------------------------");

            var header = sb.ToString();
            _logFileWriter.WriteLine(header);
            _logFileWriter.Flush();

            _writtenBytesForCurrentFile += s_utf8NoBom.GetByteCount(header)
                                         + s_utf8NoBom.GetByteCount(System.Environment.NewLine);
        }
        catch (System.Exception ex)
        {
            _provider.HandleFileError?.Invoke(new FileError(ex, _currentPath ?? "<unknown>"));
        }
    }

    /// <summary>
    /// Legacy method kept for API compatibility; now maps to daily-index roll.
    /// </summary>
    private System.String GenerateUniqueLogFileName()
    {
        // Keep the _provider.FormatLogFileName / IncludeDateInFileName if user insists,
        // but prefer our day+index scheme. We return ONLY the file name (not path).
        var (noext, ext) = GetBaseNameParts(_provider.Options.LogFileName);

        // If a custom formatter exists, try it first (non-fatal)
        if (_provider.FormatLogFileName != null)
        {
            try
            {
                var custom = _provider.FormatLogFileName(_provider.Options.LogFileName);
                if (!System.String.IsNullOrWhiteSpace(custom))
                {
                    return System.IO.Path.GetFileName(custom);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"File name formatter error: {ex.Message}");
            }
        }

        // Fallback to daily-index pattern; index will be advanced by caller if needed
        return BuildDailyIndexedName(noext, ext, System.DateTime.Now.Date, 1);
    }

    /// <summary>
    /// Legacy method: now triggers daily-index roll, non-throwing.
    /// </summary>
    private void CreateNewLogFile()
    {
        lock (_fileLock)
        {
            SafeClose_NoLock();
            _currentIndex++;
            CreateOrAdvanceStream();
        }
    }

    #endregion Private Methods

    #region Helpers

    private static (System.String noext, System.String ext) GetBaseNameParts(System.String baseName)
    {
        // Base name comes from Options.LogFileName; we treat it as the "prefix"
        // Example: "Nalix.log" -> ("Nalix", ".log")
        var ext = System.IO.Path.GetExtension(baseName);
        var noext = System.IO.Path.GetFileNameWithoutExtension(baseName);
        if (System.String.IsNullOrWhiteSpace(ext))
        {
            ext = ".log";
        }

        if (System.String.IsNullOrWhiteSpace(noext))
        {
            noext = "Nalix";
        }

        return (noext, ext);
    }

    private static System.String BuildDailyIndexedName(System.String noext, System.String ext, System.DateTime day, System.Int32 index)
        => $"{noext}_{day.ToString(DatePartFormat, System.Globalization.CultureInfo.InvariantCulture)}_{index}{ext}";

    private void SafeClose_NoLock()
    {
        try { _logFileWriter?.Flush(); } catch { /* ignore */ }
        try { _logFileWriter?.Dispose(); } catch { /* ignore */ }
        try { _logFileStream?.Dispose(); } catch { /* ignore */ }
        _logFileWriter = null;
        _logFileStream = null;
        _currentPath = null;
        _writtenBytesForCurrentFile = 0;
    }

    private void Close_NoThrow_NoLock()
    {
        try
        {
            _logFileWriter?.Flush();
            _logFileWriter?.Dispose();
            _logFileStream?.Dispose();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR closing log file: {ex.Message}");
        }
        finally
        {
            _logFileWriter = null;
            _logFileStream = null;
            _currentPath = null;
            _writtenBytesForCurrentFile = 0;
        }
    }

    #endregion Helpers
}
