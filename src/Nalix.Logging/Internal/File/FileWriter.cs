// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Environment;
using Nalix.Logging.Exceptions;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

namespace Nalix.Logging.Internal.File;

/// <summary>
/// File writer dùng bởi <see cref="FileLoggerProvider"/>.
/// Daily rolling với index và file sharing an toàn giữa nhiều process.
/// Không bao giờ throw trên IO — báo lỗi qua HandleFileError và drop gracefully.
/// </summary>
[System.Diagnostics.DebuggerDisplay("File={_currentPath,nq}, Size={_writtenBytesForCurrentFile}")]
internal sealed class FileWriter : System.IDisposable
{
    #region Constants

    private const System.Int32 WriteBufferSize = 64 * 1024; // 64 KB stream buffer

    #endregion Constants

    #region Static Fields

    // ✅ Tạo 1 lần duy nhất — tránh allocation mỗi batch
    private static readonly System.Text.UTF8Encoding s_utf8NoBom =
        new(encoderShouldEmitUTF8Identifier: false);

    // ✅ Cache số byte của newline (không đổi trong suốt lifetime)
    private static readonly System.Int32 s_newlineByteCount =
        s_utf8NoBom.GetByteCount(System.Environment.NewLine);

    #endregion Static Fields

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
        _provider = provider ?? throw new System.ArgumentNullException(nameof(provider));
        _currentIndex = 0;
        _writtenBytesForCurrentFile = 0;
        _currentDayLocal = System.DateTime.MinValue;

        lock (_fileLock)
        {
            _currentDayLocal = System.DateTime.Now.Date;
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void WriteBatch(
        System.Collections.Generic.List<LogEntry> entries,
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
                    System.String msg = formatter.Format(entry);

                    if (System.String.IsNullOrEmpty(msg))
                    {
                        continue;
                    }

                    // ✅ Tính byte count 1 lần duy nhất — dùng cho cả size check lẫn tracking
                    System.Int32 msgBytes = s_utf8NoBom.GetByteCount(msg);
                    System.Int32 totalBytes = msgBytes + s_newlineByteCount;

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
            catch (System.Exception ex)
            {
                _provider.Options.HandleFileError?.Invoke(
                    new FileError(ex, _currentPath ?? "<unknown>"));

                // Cố gắng recovery cho batch tiếp theo
                try { CLOSE_LOG_FILE_LOCKED(); } catch { /* ignore */ }
            }
        }
    }

    /// <summary>Flush buffer xuống disk ngay lập tức.</summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
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
        const System.Int32 MaxProbe = 10_000;

        try
        {
            System.IO.Directory.CreateDirectory(Directories.LogsDirectory);
        }
        catch (System.Exception ex)
        {
            _provider.Options.HandleFileError?.Invoke(new FileError(ex, Directories.LogsDirectory));
            CLOSE_LOG_FILE_LOCKED();
            return;
        }

        for (System.Int32 probe = 0; probe < MaxProbe; probe++)
        {
            if (_currentIndex <= 0)
            {
                _currentIndex = 1;
            }

            System.String filename = System.IO.Path.Combine(
                Directories.LogsDirectory,
                _provider.Options.BuildCustomFileName(_currentDayLocal, _currentIndex));

            try
            {
                System.IO.FileInfo info = new(filename);

                // File đã tồn tại và đã vượt size limit → thử index tiếp theo
                if (info.Exists && info.Length >= _provider.Options.MaxFileSizeBytes)
                {
                    _currentIndex++;
                    continue;
                }

                // Mở với FileShare.ReadWrite|Delete để nhiều process có thể dùng chung
                _stream = new System.IO.FileStream(
                    filename,
                    System.IO.FileMode.Append,
                    System.IO.FileAccess.Write,
                    System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete,
                    WriteBufferSize,
                    System.IO.FileOptions.WriteThrough);

                _writer = new System.IO.StreamWriter(_stream, s_utf8NoBom)
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
            catch (System.Exception ex)
            {
                _provider.Options.HandleFileError?.Invoke(new FileError(ex, filename));
                CLOSE_LOG_FILE_LOCKED();
                _currentIndex++;
                continue;
            }
        }

        // Hết probe → báo lỗi và drop logs cho đến lần thử tiếp theo
        _provider.Options.HandleFileError?.Invoke(new FileError(
            new System.IO.IOException("Exceeded max probes while selecting log file index."),
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
            System.Text.StringBuilder sb = new(256);
            sb.AppendLine("-----------------------------------------------------");
            sb.AppendLine($"Log File Created: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"USER: {System.Environment.UserName}");
            sb.AppendLine($"Machine: {System.Environment.MachineName}");
            sb.AppendLine($"OS: {System.Environment.OSVersion}");
            sb.AppendLine("-----------------------------------------------------");

            System.String header = sb.ToString();
            _writer.Write(header);
            _writer.Flush();

            // ✅ Fix bug cũ: AppendLine đã có newline trong header rồi
            // → chỉ cần đếm bytes của header, không cộng thêm newline
            _writtenBytesForCurrentFile += s_utf8NoBom.GetByteCount(header);
        }
        catch (System.Exception ex)
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
        System.DateTime day = System.DateTime.Now.Date;

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