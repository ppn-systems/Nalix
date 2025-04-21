using Nalix.Common.Logging;

namespace Nalix.Logging.Extensions;

public static partial class NLogixFx
{
    internal static void CreateLogEntry(
        LogLevel level,
        string message,
        string? sourceName,
        object? extendedData,
        string callerMemberName,
        string callerFilePath,
        int callerLineNumber)
    {
        if (!(level > MinimumLevel)) return;

        string fullMessage = BuildFullMessage(
            message, sourceName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

        Publisher.Publish(new LogEntry(level, EventId.Empty, fullMessage, null));
    }

    internal static string BuildFullMessage(
        string message,
        string? sourceName,
        object? extendedData,
        string callerMemberName,
        string callerFilePath,
        int callerLineNumber)
    {
        string extendedDataString = extendedData != null ? $"ExtendedData: {extendedData}" : "";

        return $"[Message]: {message}\n" +
               $"[Source]: {sourceName ?? "Unknown"}\n" +
               $"[Caller]: {callerMemberName} in {callerFilePath} at line {callerLineNumber}\n" +
               $"{extendedDataString.Trim()}";
    }
}
