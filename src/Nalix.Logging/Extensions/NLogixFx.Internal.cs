// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;

namespace Nalix.Logging.Extensions;

public static partial class NLogixFx
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static void PublishLogEntry(
        LogLevel level,
        System.String message,
        System.String? sourceName,
        System.Object? extendedData,
        System.String callerMemberName,
        System.String callerFilePath,
        System.Int32 callerLineNumber)
    {
        if (!(level > MinimumLevel))
        {
            return;
        }

        System.String fullMessage = FormatLogMessage(
            message, sourceName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

        Publisher.Publish(new LogEntry(level, EventId.Empty, fullMessage, null));
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static System.String FormatLogMessage(
        System.String message,
        System.String? sourceName,
        System.Object? extendedData,
        System.String callerMemberName,
        System.String callerFilePath, System.Int32 callerLineNumber)
    {
        return $"[Message]: {message}\n" +
               $"[Source]: {sourceName ?? "NONE"}\n" +
               $"[Caller]: {callerMemberName} in {callerFilePath} at line {callerLineNumber}\n" +
               $"{(extendedData != null ? $"ExtendedData: {extendedData}" : "").Trim()}";
    }
}
