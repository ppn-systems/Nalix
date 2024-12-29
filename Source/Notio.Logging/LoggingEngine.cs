using Notio.Common.Metadata;
using Notio.Logging.Extensions;
using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using System;
using System.Collections.Generic;

namespace Notio.Logging;

public abstract class LoggingEngine
{
    private readonly LoggingPublisher _publisher = new();
    internal FileLoggerOptions Options { get; init; } = new();

    /// <summary>
    /// Lấy đối tượng quản lý ghi nhật ký.
    /// </summary>
    public ILoggingPublisher Publisher => _publisher;

    /// <summary>
    /// Mức ghi log tối thiểu.
    /// </summary>
    public LogLevel MinimumLevel = LogLevel.Trace;

    protected bool CanLog(LogLevel level) => level >= MinimumLevel;

    /// <summary>
    /// Ghi một thông điệp với mức độ chỉ định.
    /// </summary>
    protected void CreateLogEntry(LogLevel level, EventId eventId, string message, Exception? error = null)
    {
        if (!CanLog(level)) return;
        _publisher.Publish(new LogEntry(level, eventId, message, error));
    }

    internal class LoggingPublisher : ILoggingPublisher
    {
        private readonly IList<ILoggingTarget> _targets = [];

        /// <summary>
        /// Công khai một thông điệp nhật ký.
        /// </summary>
        /// <param name="entry">Thông điệp nhật ký cần công khai.</param>
        public void Publish(LogEntry entry)
        {
            foreach (ILoggingTarget target in _targets)
                target.Publish(entry);
        }

        /// <summary>
        /// Thêm một handler ghi nhật ký.
        /// </summary>
        /// <param name="target">Handler ghi nhật ký cần thêm.</param>
        /// <returns>Instance hiện tại của <see cref="ILoggingPublisher"/>.</returns>
        public ILoggingPublisher AddTarget(ILoggingTarget target)
        {
            ArgumentNullException.ThrowIfNull(target);
            _targets.Add(target);
            return this;
        }

        /// <summary>
        /// Xóa một handler ghi nhật ký.
        /// </summary>
        /// <param name="loggerHandler">Handler ghi nhật ký cần xóa.</param>
        /// <returns>True nếu xóa thành công, ngược lại False.</returns>
        public bool RemoveTarget(ILoggingTarget loggerHandler) => _targets.Remove(loggerHandler);
    }
}