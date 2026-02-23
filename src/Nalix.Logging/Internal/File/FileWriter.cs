// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Infrastructure.Environment;
using Nalix.Logging.Internal.Exceptions;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

namespace Nalix.Logging.Internal.File;

/// <summary>
/// File writer used by <see cref="FileLoggerProvider"/>.
/// Daily rolling with index and multi-process safe file sharing.
/// Never throws on IO; reports via HandleFileError and drops gracefully.
/// </summary>
[System.Diagnostics.DebuggerDisplay("File={_currentPath,nq}, Size={_writtenBytesForCurrentFile}")]
internal sealed class FileWriter : System.IDisposable
{
    #region Constants

    private const System.Int32 WriteBufferSize = 64 * 1024;

    #endregion Constants

    #region Fields

    private readonly FileLoggerProvider _provider;
    private readonly System.Threading.Lock _fileLock = new();

    private System.Boolean _disposed;
    private System.Int32 _currentIndex;
    private System.String? _currentPath;
    private System.IO.FileStream? _stream;
    private System.IO.StreamWriter? _writer;
    private System.DateTime _currentDayLocal;
    private System.Int64 _writtenBytesForCurrentFile;

    #endregion Fields

    #region Constructors

    public FileWriter(FileLoggerProvider provider)
    {
        _currentIndex = 0;
        _writtenBytesForCurrentFile = 0;
        _currentDayLocal = System.DateTime.MinValue;
        _provider = provider ?? throw new System.ArgumentNullException(nameof(provider));
        // Initialize day/index and select file without throwing
        lock (_fileLock)
        {
            _currentDayLocal = System.DateTime.Now.Date;
            _currentIndex = 0; // CreateOrAdvanceStream will start at 1
            OpenNextLogFileLocked();
        }
    }

    #endregion Constructors

    #region APIs

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void WriteBatch(System.Collections.Generic.List<System.String> messages)
    {
        if (messages.Count == 0)
        {
            return;
        }

        lock (_fileLock)
        {
            try
            {
                EnsureLogFileIsReadyLocked();

                if (_writer is null || _stream is null)
                {
                    return; // drop silently
                }

                // UTF-8 encoder (no BOM) used for actual byte accounting
                System.Text.UTF8Encoding enc = new(encoderShouldEmitUTF8Identifier: false);
                System.Int32 newlineBytes = enc.GetByteCount(System.Environment.NewLine);

                foreach (System.String msg in messages)
                {
                    if (System.String.IsNullOrEmpty(msg))
                    {
                        continue;
                    }

                    System.Int32 bytes = enc.GetByteCount(msg) + newlineBytes;

                    // roll if will exceed size
                    if (_writtenBytesForCurrentFile + bytes > _provider.Options.MaxFileSizeBytes)
                    {
                        CloseLogFileLocked();
                        _currentIndex++;
                        OpenNextLogFileLocked();
                        if (_writer is null || _stream is null)
                        {
                            return; // drop rest silently
                        }
                    }

                    _writer.WriteLine(msg);
                    _writtenBytesForCurrentFile += bytes;
                }

                // Flush once per batch; writer buffer keeps syscalls low
                _writer.Flush();
            }
            catch (System.Exception ex)
            {
                _provider.Options.HandleFileError?.Invoke(new FileError(ex, _currentPath ?? "<unknown>"));
                // try to recover next batch
                try
                {
                    CloseLogFileLocked();
                }
                catch { /* ignore */ }
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
            try
            {
                _writer?.Flush();
            }
            catch { /* ignore */ }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_fileLock)
        {
            CloseLogFileLocked();
        }
    }

    #endregion APIs

    #region Private methods

    private void CloseLogFileLocked()
    {
        try
        {
            _writer?.Flush();
        }
        catch { /* ignore */ }
        try
        {
            _writer?.Dispose();
        }
        catch { /* ignore */ }
        try
        {
            _stream?.Dispose();
        }
        catch { /* ignore */ }

        _writer = null;
        _stream = null;
        _currentPath = null;
        _writtenBytesForCurrentFile = 0;
    }

    // Pick or create a file for the current day, probing indices, never throwing.
    private void OpenNextLogFileLocked()
    {
        const System.Int32 MaxProbe = 10000;

        try
        {
            _ = System.IO.Directory.CreateDirectory(Directories.LogsDirectory);
        }
        catch (System.Exception ex)
        {
            _provider.Options.HandleFileError?.Invoke(new FileError(ex, Directories.LogsDirectory));
            CloseLogFileLocked();
            return;
        }

        for (System.Int32 probe = 0; probe < MaxProbe; probe++)
        {
            if (_currentIndex <= 0)
            {
                _currentIndex = 1;
            }

            System.String fullPath = System.IO.Path.Combine(Directories.LogsDirectory, _provider.Options
                                                   .BuildFileName(_currentDayLocal, _currentIndex));

            try
            {
                System.IO.FileInfo info = new(fullPath);
                // If file exists and already beyond size, skip to next index
                if (info.Exists && info.Length >= _provider.Options.MaxFileSizeBytes)
                {
                    _currentIndex++;
                    continue;
                }

                // Try append with cooperative share for multi-process
                _stream = new System.IO.FileStream(
                    fullPath,
                    System.IO.FileMode.Append,
                    System.IO.FileAccess.Write,
                    System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete,
                    WriteBufferSize,
                    System.IO.FileOptions.WriteThrough);

                _writer = new System.IO.StreamWriter(_stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                {
                    AutoFlush = false
                };

                _currentPath = fullPath;
                _writtenBytesForCurrentFile = info.Exists ? info.Length : 0;

                // Write header only if new file (length == 0)
                if (!info.Exists || info.Length == 0)
                {
                    WriteLogFileHeaderLocked();
                }

                return; // success
            }
            catch (System.Exception ex)
            {
                _provider.Options.HandleFileError?.Invoke(new FileError(ex, fullPath));
                CloseLogFileLocked();
                _currentIndex++;
                continue;
            }
        }

        // Give up for now; drop logs until next attempt
        _provider.Options.HandleFileError?.Invoke(new FileError(new System.IO.IOException("Exceeded max probes while selecting log file index."), Directories.LogsDirectory));
        CloseLogFileLocked();
    }

    private void WriteLogFileHeaderLocked()
    {
        try
        {
            if (_writer is null)
            {
                return;
            }

            System.Text.StringBuilder sb = new(256);
            _ = sb.AppendLine("-----------------------------------------------------");
            _ = sb.AppendLine($"Log File Created: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            _ = sb.AppendLine($"USER: {System.Environment.UserName}");
            _ = sb.AppendLine($"Machine: {System.Environment.MachineName}");
            _ = sb.AppendLine($"OS: {System.Environment.OSVersion}");
            _ = sb.AppendLine("-----------------------------------------------------");
            System.String txt = sb.ToString();

            _writer.WriteLine(txt);
            _writer.Flush();

            // Count UTF-8 header bytes correctly
            System.Text.UTF8Encoding enc = new(false);
            _writtenBytesForCurrentFile += enc.GetByteCount(txt) + enc.GetByteCount(System.Environment.NewLine);
        }
        catch (System.Exception ex)
        {
            _provider.Options.HandleFileError?.Invoke(new FileError(ex, _currentPath ?? "<unknown>"));
        }
    }

    // Ensure stream is open for the correct day and size; do not throw.
    private void EnsureLogFileIsReadyLocked()
    {
        System.DateTime now = System.DateTime.Now;
        System.DateTime day = now.Date;

        if (day != _currentDayLocal)
        {
            // new day: reset and start at _1
            CloseLogFileLocked();
            _currentDayLocal = day;
            _currentIndex = 0;
            _writtenBytesForCurrentFile = 0;
            OpenNextLogFileLocked();
            return;
        }

        if (_stream is null || _writer is null)
        {
            OpenNextLogFileLocked();
            return;
        }

        if (_writtenBytesForCurrentFile >= _provider.Options.MaxFileSizeBytes)
        {
            CloseLogFileLocked();
            _currentIndex++;
            OpenNextLogFileLocked();
        }
    }

    #endregion Private methods
}
