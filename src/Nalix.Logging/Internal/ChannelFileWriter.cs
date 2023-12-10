// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Logging.Internal.Exceptions;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Nalix.Logging.Internal;

/// <summary>
/// File writer used by <see cref="ChannelFileLoggerProvider"/>.
/// Minimizes lock scope and batches writes.
/// </summary>
[DebuggerDisplay("File={_provider.Options.LogFileName,nq}, Size={_currentFileSize}")]
internal sealed class ChannelFileWriter : System.IDisposable
{
    private const System.Int32 WriteBufferSize = 8 * 1024;

    private readonly ChannelFileLoggerProvider _provider;
    private readonly Lock _fileLock = new();

    private System.Boolean _disposed;
    private System.Int64 _currentFileSize;
    private FileStream? _stream;
    private StreamWriter? _writer;

    public ChannelFileWriter(ChannelFileLoggerProvider provider)
    {
        _provider = provider ?? throw new System.ArgumentNullException(nameof(provider));
        EnsureDirectoryExists();
        OpenFile(_provider.Options.Append);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void WriteBatch(System.Collections.Generic.List<System.String> messages)
    {
        if (messages.Count == 0)
        {
            return;
        }

        lock (_fileLock)
        {
            if (_writer is null || _stream is null)
            {
                OpenFile(_provider.Options.Append);
                if (_writer is null || _stream is null)
                {
                    return;
                }
            }

            // Write each message; rely on StreamWriter internal buffer.
            foreach (var msg in messages)
            {
                if (System.String.IsNullOrEmpty(msg))
                {
                    continue;
                }

                // approximate size (UTF-16 char count * 2) + newline
                var size = (msg.Length * sizeof(System.Char)) + (System.Environment.NewLine.Length * sizeof(System.Char));
                if (_currentFileSize + size > _provider.Options.MaxFileSizeBytes)
                {
                    CreateNewLogFile_NoLock();
                    if (_writer is null || _stream is null)
                    {
                        return;
                    }
                }

                _writer.WriteLine(msg);
                _currentFileSize += size;
            }

            // Flush once per batch
            _writer.Flush();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void Flush()
    {
        lock (_fileLock)
        {
            _writer?.Flush();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Close_NoLock();
    }

    #region Private helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void EnsureDirectoryExists()
    {
        try
        {
            var dir = _provider.Options.LogDirectory;
            if (!Directory.Exists(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"Create log dir failed: {ex.Message}");
            try
            {
                var temp = Path.Combine(Path.GetTempPath(), "assets", "logs");
                _ = Directory.CreateDirectory(temp);
                _provider.Options.LogDirectory = temp;
            }
            catch
            {
                _provider.Options.LogDirectory = ".";
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void OpenFile(System.Boolean append)
    {
        lock (_fileLock)
        {
            CreateLogFileStream_NoLock(append);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void CreateLogFileStream_NoLock(System.Boolean append)
    {
        var path = Path.Combine(_provider.Options.LogDirectory, _provider.Options.LogFileName);

        try
        {
            var exists = File.Exists(path);
            _currentFileSize = exists ? new FileInfo(path).Length : 0;

            if (exists && _currentFileSize > 0 && _currentFileSize >= _provider.Options.MaxFileSizeBytes)
            {
                CreateNewLogFile_NoLock();
                return;
            }

            _stream = new FileStream(
                path,
                append ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                WriteBufferSize,
                FileOptions.WriteThrough);

            _writer = new StreamWriter(_stream, Encoding.UTF8, WriteBufferSize)
            {
                AutoFlush = false
            };

            if (!append || _currentFileSize == 0)
            {
                var sb = new StringBuilder(256);
                _ = sb.AppendLine("-----------------------------------------------------");
                _ = sb.AppendLine($"Log File Created: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                _ = sb.AppendLine($"User: {System.Environment.UserName}");
                _ = sb.AppendLine($"Machine: {System.Environment.MachineName}");
                _ = sb.AppendLine($"OS: {System.Environment.OSVersion}");
                _ = sb.AppendLine("-----------------------------------------------------");
                _writer.WriteLine(sb.ToString());
                _writer.Flush();
                _currentFileSize = _stream.Length;
            }
        }
        catch (System.Exception ex)
        {
            _provider.Options.HandleFileError?.Invoke(new FileError(ex, path));

            _writer?.Dispose();
            _stream?.Dispose();
            _writer = null;
            _stream = null;
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void CreateNewLogFile_NoLock()
    {
        Close_NoLock();
        _provider.Options.LogFileName = GenerateUniqueLogFileName_NoLock();
        CreateLogFileStream_NoLock(append: false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private System.String GenerateUniqueLogFileName_NoLock()
    {
        System.String baseName = _provider.Options.LogFileName;
        System.String ext = Path.GetExtension(baseName);
        System.String noext = Path.GetFileNameWithoutExtension(baseName);

        System.String file = baseName;

        if (_provider.Options.FormatLogFileName != null)
        {
            try { file = _provider.Options.FormatLogFileName(baseName); }
            catch (System.Exception ex) { Debug.WriteLine($"File name formatter error: {ex.Message}"); }
        }
        else if (_provider.Options.IncludeDateInFileName)
        {
            var now = System.DateTime.Now;
            file = $"{noext}_{now:yyyy-MM-dd}_{System.Environment.TickCount & 0xFFFF}{ext}";
        }

        System.String dir = _provider.Options.LogDirectory;
        System.String full = Path.Combine(dir, file);
        System.Int32 unique = 0;
        while (File.Exists(full) && unique < 10000)
        {
            unique++;
            System.String candidate = $"{noext}_{System.DateTime.Now:yyyy-MM-dd}_{unique}{ext}";
            full = Path.Combine(dir, candidate);
        }
        if (unique >= 10000)
        {
            full = Path.Combine(dir, $"{noext}_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}_{System.Guid.NewGuid().ToString()[..8]}{ext}");
        }
        return Path.GetFileName(full);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Close_NoLock()
    {
        lock (_fileLock)
        {
            try
            {
                _writer?.Flush();
                _writer?.Dispose();
                _stream?.Dispose();
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Close file failed: {ex.Message}");
            }
            finally
            {
                _writer = null;
                _stream = null;
                _currentFileSize = 0;
            }
        }
    }

    #endregion
}