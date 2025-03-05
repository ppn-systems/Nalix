// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Environment;
using Nalix.Logging.Internal.Exceptions;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

namespace Nalix.Logging.Internal.File;

/// <summary>
/// File writer used by <see cref="ChannelFileLoggerProvider"/>.
/// Daily rolling with index and multi-process safe file sharing.
/// Never throws on IO; reports via HandleFileError and drops gracefully.
/// </summary>
[DebuggerDisplay("File={_currentPath,nq}, Size={_writtenBytesForCurrentFile}")]
internal sealed class ChannelFileWriter : IDisposable
{
    private const Int32 WriteBufferSize = 64 * 1024;

    private readonly ChannelFileLoggerProvider _provider;
    private readonly Lock _fileLock = new();

    private Boolean _disposed;
    private FileStream? _stream;
    private StreamWriter? _writer;

    private DateTime _currentDayLocal = DateTime.MinValue;
    private Int32 _currentIndex = 0;
    private Int64 _writtenBytesForCurrentFile = 0;
    private String? _currentPath;

    public ChannelFileWriter(ChannelFileLoggerProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        // Initialize day/index and select file without throwing
        lock (_fileLock)
        {
            _currentDayLocal = DateTime.Now.Date;
            _currentIndex = 0; // CreateOrAdvanceStream will start at 1
            CreateOrAdvanceStream();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void WriteBatch(System.Collections.Generic.List<String> messages)
    {
        if (messages.Count == 0)
        {
            return;
        }

        lock (_fileLock)
        {
            try
            {
                EnsureStreamReady_NoLock();

                if (_writer is null || _stream is null)
                {
                    return; // drop silently
                }

                // UTF-8 encoder (no BOM) used for actual byte accounting
                var enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                var newlineBytes = enc.GetByteCount(Environment.NewLine);

                foreach (var msg in messages)
                {
                    if (String.IsNullOrEmpty(msg))
                    {
                        continue;
                    }

                    var bytes = enc.GetByteCount(msg) + newlineBytes;

                    // roll if will exceed size
                    if (_writtenBytesForCurrentFile + bytes > _provider.Options.MaxFileSizeBytes)
                    {
                        SafeClose_NoLock();
                        _currentIndex++;
                        CreateOrAdvanceStream();
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
            catch (Exception ex)
            {
                _provider.Options.HandleFileError?.Invoke(new FileError(ex, _currentPath ?? "<unknown>"));
                // try to recover next batch
                try { SafeClose_NoLock(); } catch { /* ignore */ }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void Flush()
    {
        lock (_fileLock)
        {
            try { _writer?.Flush(); } catch { /* ignore */ }
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
            SafeClose_NoLock();
        }
    }

    #region Private helpers

    // Ensure stream is open for the correct day and size; do not throw.
    private void EnsureStreamReady_NoLock()
    {
        var now = DateTime.Now;
        var day = now.Date;

        if (day != _currentDayLocal)
        {
            // new day: reset and start at _1
            SafeClose_NoLock();
            _currentDayLocal = day;
            _currentIndex = 0;
            _writtenBytesForCurrentFile = 0;
            CreateOrAdvanceStream();
            return;
        }

        if (_stream is null || _writer is null)
        {
            CreateOrAdvanceStream();
            return;
        }

        if (_writtenBytesForCurrentFile >= _provider.Options.MaxFileSizeBytes)
        {
            SafeClose_NoLock();
            _currentIndex++;
            CreateOrAdvanceStream();
        }
    }

    // Pick or create a file for the current day, probing indices, never throwing.
    private void CreateOrAdvanceStream()
    {
        try
        {
            _ = Directory.CreateDirectory(Directories.LogsDirectory);
        }
        catch (Exception ex)
        {
            _provider.Options.HandleFileError?.Invoke(new FileError(ex, Directories.LogsDirectory));
            SafeClose_NoLock();
            return;
        }

        const Int32 MaxProbe = 10000;
        for (Int32 probe = 0; probe < MaxProbe; probe++)
        {
            if (_currentIndex <= 0)
            {
                _currentIndex = 1;
            }

            var fileName = _provider.Options.BuildFileName(_currentDayLocal, _currentIndex);
            var fullPath = Path.Combine(Directories.LogsDirectory, fileName);

            try
            {
                var info = new FileInfo(fullPath);
                // If file exists and already beyond size, skip to next index
                if (info.Exists && info.Length >= _provider.Options.MaxFileSizeBytes)
                {
                    _currentIndex++;
                    continue;
                }

                // Try append with cooperative share for multi-process
                _stream = new FileStream(
                    fullPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete,
                    WriteBufferSize,
                    FileOptions.WriteThrough);

                _writer = new StreamWriter(_stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                {
                    AutoFlush = false
                };

                _currentPath = fullPath;
                _writtenBytesForCurrentFile = info.Exists ? info.Length : 0;

                // Write header only if new file (length == 0)
                if (!info.Exists || info.Length == 0)
                {
                    WriteFileHeader_NoLock();
                }

                return; // success
            }
            catch (Exception ex)
            {
                _provider.Options.HandleFileError?.Invoke(new FileError(ex, fullPath));
                SafeClose_NoLock();
                _currentIndex++;
                continue;
            }
        }

        // Give up for now; drop logs until next attempt
        _provider.Options.HandleFileError?.Invoke(new FileError(
            new IOException("Exceeded max probes while selecting log file index."),
            Directories.LogsDirectory));
        SafeClose_NoLock();
    }

    private void WriteFileHeader_NoLock()
    {
        try
        {
            if (_writer is null)
            {
                return;
            }

            var sb = new StringBuilder(256);
            _ = sb.AppendLine("-----------------------------------------------------");
            _ = sb.AppendLine($"Log File Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            _ = sb.AppendLine($"User: {Environment.UserName}");
            _ = sb.AppendLine($"Machine: {Environment.MachineName}");
            _ = sb.AppendLine($"OS: {Environment.OSVersion}");
            _ = sb.AppendLine("-----------------------------------------------------");
            var txt = sb.ToString();

            _writer.WriteLine(txt);
            _writer.Flush();

            // Count UTF-8 header bytes correctly
            var enc = new UTF8Encoding(false);
            _writtenBytesForCurrentFile += enc.GetByteCount(txt) + enc.GetByteCount(Environment.NewLine);
        }
        catch (Exception ex)
        {
            _provider.Options.HandleFileError?.Invoke(new FileError(ex, _currentPath ?? "<unknown>"));
        }
    }

    private void SafeClose_NoLock()
    {
        try { _writer?.Flush(); } catch { /* ignore */ }
        try { _writer?.Dispose(); } catch { /* ignore */ }
        try { _stream?.Dispose(); } catch { /* ignore */ }
        _writer = null;
        _stream = null;
        _currentPath = null;
        _writtenBytesForCurrentFile = 0;
    }

    #endregion
}
