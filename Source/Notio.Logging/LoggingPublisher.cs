using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using System.Collections.Generic;

namespace Notio.Logging;

public class LoggingPublisher : ILogingPublisher
{
    private readonly IList<ILoggingSinks> loggerSinks;

    /// <summary>
    /// Khởi tạo một <see cref="LoggingPublisher"/> mới.
    /// </summary>
    public LoggingPublisher()
    {
        loggerSinks = [];
    }

    /// <summary>
    /// Công khai một thông điệp nhật ký.
    /// </summary>
    /// <param name="logMessage">Thông điệp nhật ký cần công khai.</param>
    public void Publish(LogMessage logMessage)
    {
        foreach (ILoggingSinks loggerHandler in loggerSinks)
            loggerHandler.Publish(logMessage);
    }

    /// <summary>
    /// Thêm một handler ghi nhật ký.
    /// </summary>
    /// <param name="loggerHandler">Handler ghi nhật ký cần thêm.</param>
    /// <returns>Instance hiện tại của <see cref="ILogingPublisher"/>.</returns>
    public ILogingPublisher AddHandler(ILoggingSinks loggerHandler)
    {
        if (loggerHandler != null) loggerSinks.Add(loggerHandler);
        return this;
    }

    /// <summary>
    /// Xóa một handler ghi nhật ký.
    /// </summary>
    /// <param name="loggerHandler">Handler ghi nhật ký cần xóa.</param>
    /// <returns>True nếu xóa thành công, ngược lại False.</returns>
    public bool RemoveHandler(ILoggingSinks loggerHandler)
    {
        return loggerSinks.Remove(loggerHandler);
    }
}