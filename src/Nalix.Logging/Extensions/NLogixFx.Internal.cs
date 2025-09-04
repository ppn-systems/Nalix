// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Models;

namespace Nalix.Logging.Extensions;

public static partial class NLogixFx
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static void CreateLogEntry(
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

        System.String fullMessage = BuildFullMessage(
            message, sourceName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

        Publisher.Publish(new LogEntry(level, EventId.Empty, fullMessage, null));
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static System.String BuildFullMessage(
        System.String message, System.String? sourceName, System.Object? extendedData,
        System.String callerMemberName, System.String callerFilePath, System.Int32 callerLineNumber)
    {
        System.String extendedDataString = extendedData != null ? $"ExtendedData: {extendedData}" : "";

        return $"[Message]: {message}\n" +
               $"[Source]: {sourceName ?? "NONE"}\n" +
               $"[Caller]: {callerMemberName} in {callerFilePath} at line {callerLineNumber}\n" +
               $"{extendedDataString.Trim()}";
    }
}
