using System;
using System.IO;

namespace Notio.Logging.Storage;

/// <summary>
/// Lớp quản lý ghi log vào tệp tin.
/// </summary>
internal class FileWriter
{
    private int _count = 0;

    private FileStream? _logFileStream;
    private StreamWriter? _logFileWriter;
    private readonly FileLoggerProvider _fileLogProvider;

    /// <summary>
    /// Khởi tạo một instance mới của <see cref="FileWriter"/>.
    /// </summary>
    /// <param name="fileLogProvider">Nhà cung cấp file log.</param>
    internal FileWriter(FileLoggerProvider fileLogProvider)
    {
        _fileLogProvider = fileLogProvider;
        this.OpenFile(_fileLogProvider.Append);
    }

    /// <summary>
    /// Lấy tên cơ sở của tệp log.
    /// </summary>
    /// <returns>Tên tệp log cơ sở.</returns>
    private string GetBaseLogFileName()
    {
        string fileName = this.GenerateUniqueLogFileName();
        return Path.Combine(_fileLogProvider.LogDirectory, _fileLogProvider.FormatLogFileName?.Invoke(fileName) ?? fileName);
    }

    /// <summary>
    /// Tạo dòng tệp log.
    /// </summary>
    /// <param name="append">Chế độ nối thêm vào tệp log hiện có hay không.</param>
    private void CreateLogFileStream(bool append)
    {
        string logFilePath = this.GetBaseLogFileName();
        Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);

        // Nếu append là true, tiếp tục nối vào cuối tệp
        // Nếu append là false, tạo tệp mới từ đầu
        _logFileStream = new FileStream(logFilePath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);

        _logFileWriter = new StreamWriter(_logFileStream)
        {
            AutoFlush = true // Đảm bảo ghi liền vào tệp khi cần
        };

        // Nếu tệp vượt quá kích thước tối đa, cần tạo tệp mới
        if (_logFileStream.Length >= _fileLogProvider.MaxFileSize && append)
        {
            this.CreateNewLogFileDirectory();
        }
    }


    /// <summary>
    /// Tạo thư mục tệp log mới.
    /// </summary>
    private void CreateNewLogFileDirectory()
    {
        // Khi tệp hiện tại quá lớn, tạo tệp mới trong thư mục log.
        string newFileName = this.GenerateUniqueLogFileName();
        this.UseNewLogFile(Path.Combine(_fileLogProvider.LogDirectory, newFileName));
    }

    private string GenerateUniqueLogFileName()
    {
        string newFileName;

        do
        {
            newFileName = $"{_fileLogProvider.LogFileName}_{_count++}.log";
        } while (System.IO.File.Exists(Path.Combine(_fileLogProvider.LogDirectory, newFileName)));

        if (_count != 0)
        {
            _count -= 2;
            if (_count < 0) _count = 0;
            return Path.Combine(_fileLogProvider.LogDirectory, $"{_fileLogProvider.LogFileName}_{_count}.log");
        }

        return newFileName;
    }

    /// <summary>
    /// Sử dụng tệp log mới.
    /// </summary>
    /// <param name="newLogFileName">Tên tệp log mới.</param>
    internal void UseNewLogFile(string newLogFileName)
    {
        _fileLogProvider.LogFileName = Path.GetFileName(newLogFileName);
        this.CreateLogFileStream(_fileLogProvider.Append);
    }

    /// <summary>
    /// Mở tệp log.
    /// </summary>
    /// <param name="append">Chế độ nối thêm vào tệp log hiện có hay không.</param>
    private void OpenFile(bool append)
    {
        try
        {
            this.CreateLogFileStream(append);
            if (_logFileStream!.Length >= _fileLogProvider.MaxFileSize && append)
            {
                this.CreateNewLogFileDirectory();
            }
        }
        catch (Exception ex)
        {
            _fileLogProvider.HandleFileError?.Invoke(new FileError(_fileLogProvider.LogFileName, ex));
        }
    }

    /// <summary>
    /// Ghi thông điệp vào tệp log.
    /// </summary>
    /// <param name="message">Thông điệp để ghi.</param>
    /// <param name="flush">Có làm tươi bộ đệm hay không.</param>
    internal void WriteMessage(string message, bool flush)
    {
        _logFileWriter?.WriteLine(message);
        if (flush) _logFileWriter?.Flush();
    }

    /// <summary>
    /// Đóng tệp log.
    /// </summary>
    internal void Close()
    {
        _logFileWriter?.Dispose();
        _logFileStream?.Dispose();
    }
}