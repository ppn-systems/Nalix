using Notio.Logging.Metadata;
using System;

namespace Notio.Logging.Interfaces;

public interface ILoggerFormatter
{
    string FormatLog(LogMessage logMsg, DateTime timeStamp);
}