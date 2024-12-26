using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using System.Collections.Generic;

namespace Notio.Logging.Base;

public class LoggerPublisher : ILoggerPublisher
{
    private readonly IList<ILoggerSinks> loggerSinks;

    /// <summary>
    /// Khởi tạo một <see cref="LoggerPublisher"/> mới.
    /// </summary>
    public LoggerPublisher()
    {
        loggerSinks = [];
    }

    /// <summary>
    /// Công khai một thông điệp nhật ký.
    /// </summary>
    /// <param name="logMessage">Thông điệp nhật ký cần công khai.</param>
    public void Publish(LogMessage logMessage)
    {
        foreach (ILoggerSinks loggerHandler in loggerSinks)
            loggerHandler.Publish(logMessage);
    }

    /// <summary>
    /// Thêm một handler ghi nhật ký.
    /// </summary>
    /// <param name="loggerHandler">Handler ghi nhật ký cần thêm.</param>
    /// <returns>Instance hiện tại của <see cref="ILoggerPublisher"/>.</returns>
    public ILoggerPublisher AddHandler(ILoggerSinks loggerHandler)
    {
        if (loggerHandler != null) loggerSinks.Add(loggerHandler);
        return this;
    }

    /// <summary>
    /// Xóa một handler ghi nhật ký.
    /// </summary>
    /// <param name="loggerHandler">Handler ghi nhật ký cần xóa.</param>
    /// <returns>True nếu xóa thành công, ngược lại False.</returns>
    public bool RemoveHandler(ILoggerSinks loggerHandler)
    {
        return loggerSinks.Remove(loggerHandler);
    }
}