// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Nalix.Common.Diagnostics;
using Nalix.Common.Environment;
using Nalix.Logging.Exceptions;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

namespace Nalix.Logging.Internal.File;

/// <summary>
/// File writer dùng bởi <see cref="FileLoggerProvider"/>.
/// Daily rolling với index và file sharing an toàn giữa nhiều process.
/// Không bao giờ throw trên IO — báo lỗi qua HandleFileError và drop gracefully.
/// </summary>
[DebuggerDisplay("File={_currentPath,nq}, Size={_writtenBytesForCurrentFile}")]
internal sealed class FileWriter : IDisposable
{
    #region Constants

    /// <summary>
    /// 64 KB stream buffer
    /// </summary>
    private const int WriteBufferSize = 64 * 1024;

    #endregion Constants

    #region Static Fields

    /// <summary>
    /// ✅ Tạo 1 lần duy nhất — tránh allocation mỗi batch
    /// </summary>
    private static readonly UTF8Encoding s_utf8NoBom =
        new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// ✅ Cache số byte của newline (không đổi trong suốt lifetime)
    /// </summary>
    private static readonly int s_newlineByteCount =
        s_utf8NoBom.GetByteCount(Environment.NewLine);

    #endregion Static Fields

    #region Fields

    private readonly FileLoggerProvider _provider;
    private readonly Lock _fileLock = new();

    private bool _disposed;
    private int _currentIndex;
    private string? _currentPath;
    private FileStream? _stream;
    private StreamWriter? _writer;
    private DateTime _currentDayLocal;
    private long _writtenBytesForCurrentFile;

    #endregion Fields

    #region Constructors

    public FileWriter(FileLoggerProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _currentIndex = 0;
        _writtenBytesForCurrentFile = 0;
        _currentDayLocal = DateTime.MinValue;

        lock (_fileLock)
        {
            _currentDayLocal = DateTime.Now.Date;
            _currentIndex = 0;
            OPEN_NEXT_LOG_FILE_LOCKED();
        }
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Ghi một batch <see cref="LogEntry"/> vào file.
    /// Format xảy ra ở đây (consumer thread) — không có contention với producer.
    /// </summary>
    /// <param name="entries">Danh sách entries cần ghi.</param>
    /// <param name="formatter">Formatter dùng để chuyển entry thành string.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    internal void WriteBatch(
        List<LogEntry> entries,
        ILoggerFormatter formatter)
    {
        if (entries.Count == 0)
        {
            return;
        }

        lock (_fileLock)
        {
            try
            {
                ENSURE_LOG_FILE_IS_READY_LOCKED();

                if (_writer is null || _stream is null)
                {
                    return; // drop silently
                }

                foreach (LogEntry entry in entries)
                {
                    // ✅ Format tại đây — single-threaded, không contention
                    string msg = formatter.Format(entry);

                    if (string.IsNullOrEmpty(msg))
                    {
                        continue;
                    }

                    // ✅ Tính byte count 1 lần duy nhất — dùng cho cả size check lẫn tracking
                    int msgBytes = s_utf8NoBom.GetByteCount(msg);
                    int totalBytes = msgBytes + s_newlineByteCount;

                    // Roll file nếu sẽ vượt quá giới hạn kích thước
                    if (_writtenBytesForCurrentFile + totalBytes > _provider.Options.MaxFileSizeBytes)
                    {
                        CLOSE_LOG_FILE_LOCKED();
                        _currentIndex++;
                        OPEN_NEXT_LOG_FILE_LOCKED();

                        if (_writer is null || _stream is null)
                        {
                            return; // không mở được file mới → drop phần còn lại
                        }
                    }

                    _writer.WriteLine(msg);
                    _writtenBytesForCurrentFile += totalBytes;
                }

                // ✅ Flush 1 lần sau toàn bộ batch — giảm số lần syscall
                _writer.Flush();
            }
            catch (Exception ex)
            {
                _provider.Options.HandleFileError?.Invoke(
                    new FileError(ex, _currentPath ?? "<unknown>"));

                // Cố gắng recovery cho batch tiếp theo
                try { CLOSE_LOG_FILE_LOCKED(); } catch { /* ignore */ }
            }
        }
    }

    /// <summary>Flush buffer xuống disk ngay lập tức.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    internal void Flush()
    {
        lock (_fileLock)
        {
            try { _writer?.Flush(); }
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
            CLOSE_LOG_FILE_LOCKED();
        }
    }

    #endregion APIs

    #region Private Methods

    private void CLOSE_LOG_FILE_LOCKED()
    {
        try { _writer?.Flush(); } catch { /* ignore */ }
        try { _writer?.Dispose(); } catch { /* ignore */ }
        try { _stream?.Dispose(); } catch { /* ignore */ }

        _writer = null;
        _stream = null;
        _currentPath = null;
        _writtenBytesForCurrentFile = 0;
    }

    /// <summary>
    /// Chọn hoặc tạo file log cho ngày hiện tại, thử từng index tuần tự.
    /// Không bao giờ throw.
    /// </summary>
    private void OPEN_NEXT_LOG_FILE_LOCKED()
    {
        const int MaxProbe = 10_000;

        try
        {
            _ = Directory.CreateDirectory(Directories.LogsDirectory);
        }
        catch (Exception ex)
        {
            _provider.Options.HandleFileError?.Invoke(new FileError(ex, Directories.LogsDirectory));
            CLOSE_LOG_FILE_LOCKED();
            return;
        }

        for (int probe = 0; probe < MaxProbe; probe++)
        {
            if (_currentIndex <= 0)
            {
                _currentIndex = 1;
            }

            string filename = Path.Combine(
                Directories.LogsDirectory,
                _provider.Options.BuildCustomFileName(_currentDayLocal, _currentIndex));

            try
            {
                FileInfo info = new(filename);

                // File đã tồn tại và đã vượt size limit → thử index tiếp theo
                if (info.Exists && info.Length >= _provider.Options.MaxFileSizeBytes)
                {
                    _currentIndex++;
                    continue;
                }

                // Mở với FileShare.ReadWrite|Delete để nhiều process có thể dùng chung
                _stream = new FileStream(
                    filename,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete,
                    WriteBufferSize,
                    FileOptions.WriteThrough);

                _writer = new StreamWriter(_stream, s_utf8NoBom)
                {
                    AutoFlush = false // Flush thủ công sau mỗi batch
                };

                _currentPath = filename;
                _writtenBytesForCurrentFile = info.Exists ? info.Length : 0;

                // Ghi header chỉ khi file mới tạo
                if (!info.Exists || info.Length == 0)
                {
                    WRITE_LOG_FILE_HEADER_LOCKED();
                }

                return; // Thành công
            }
            catch (Exception ex)
            {
                _provider.Options.HandleFileError?.Invoke(new FileError(ex, filename));
                CLOSE_LOG_FILE_LOCKED();
                _currentIndex++;
            }
        }

        // Hết probe → báo lỗi và drop logs cho đến lần thử tiếp theo
        _provider.Options.HandleFileError?.Invoke(new FileError(
            new IOException("Exceeded max probes while selecting log file index."),
            Directories.LogsDirectory));

        CLOSE_LOG_FILE_LOCKED();
    }

    private void WRITE_LOG_FILE_HEADER_LOCKED()
    {
        if (_writer is null)
        {
            return;
        }

        try
        {
            StringBuilder sb = new(256);
            _ = sb.AppendLine("-----------------------------------------------------");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Log File Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"USER: {Environment.UserName}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Machine: {Environment.MachineName}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"OS: {Environment.OSVersion}");
            _ = sb.AppendLine("-----------------------------------------------------");

            string header = sb.ToString();
            _writer.Write(header);
            _writer.Flush();

            // ✅ Fix bug cũ: AppendLine đã có newline trong header rồi
            // → chỉ cần đếm bytes của header, không cộng thêm newline
            _writtenBytesForCurrentFile += s_utf8NoBom.GetByteCount(header);
        }
        catch (Exception ex)
        {
            _provider.Options.HandleFileError?.Invoke(
                new FileError(ex, _currentPath ?? "<unknown>"));
        }
    }

    /// <summary>
    /// Kiểm tra stream có đang mở cho đúng ngày và chưa vượt size không.
    /// Nếu cần thì roll file. Không throw.
    /// </summary>
    private void ENSURE_LOG_FILE_IS_READY_LOCKED()
    {
        DateTime day = DateTime.Now.Date;

        // Sang ngày mới → reset và mở file mới
        if (day != _currentDayLocal)
        {
            CLOSE_LOG_FILE_LOCKED();
            _currentDayLocal = day;
            _currentIndex = 0;
            _writtenBytesForCurrentFile = 0;
            OPEN_NEXT_LOG_FILE_LOCKED();
            return;
        }

        // Stream bị đóng (lỗi trước đó) → thử mở lại
        if (_stream is null || _writer is null)
        {
            OPEN_NEXT_LOG_FILE_LOCKED();
            return;
        }

        // Đã vượt size limit → roll sang file tiếp theo
        if (_writtenBytesForCurrentFile >= _provider.Options.MaxFileSizeBytes)
        {
            CLOSE_LOG_FILE_LOCKED();
            _currentIndex++;
            OPEN_NEXT_LOG_FILE_LOCKED();
        }
    }

    #endregion Private Methods
}
