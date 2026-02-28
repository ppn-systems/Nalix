// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;

namespace Nalix.Logging.Extensions;

public static partial class NLogixFx
{
    private const System.String Sep = "================================================================================";

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void PUBLISH_LOG_ENTRY(
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

        System.String fullMessage = FORMAT_LOG_MESSAGE(
            message, sourceName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

        Publisher.Publish(new LogEntry(level, EventId.Empty, fullMessage, null));
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static System.String FORMAT_LOG_MESSAGE(
        System.String message,
        System.String? sourceName,
        System.Object? extendedData,
        System.String callerMemberName,
        System.String callerFilePath, System.Int32 callerLineNumber)
    {
        return
            $"{Sep}\n" +
            $"Source     : {sourceName ?? "NONE"}\n" +
            $"Caller     : {callerMemberName} ({callerFilePath}:{callerLineNumber})\n" +
            $"Data       : {FORMAT_EXTENDED_DATA(extendedData)}\n" +
            $"Message    : {message}\n" +
            $"{Sep}\n";
    }

    private static System.String FORMAT_EXTENDED_DATA(System.Object? extendedData)
    {
        const System.Int32 MaxLen = 200;

        if (extendedData is null)
        {
            return "-";
        }

        try
        {
            if (extendedData is System.Exception ex)
            {
                System.String m = ex.Message ?? "";
                return $"{ex.GetType().Name}: {m}";
            }

            System.String s = extendedData.ToString() ?? "-";
            // Replace new lines to keep single-line appearance and trim long payloads
            s = s.Replace("\r", " ").Replace("\n", " ");
            if (s.Length > MaxLen)
            {
                s = s[..MaxLen] + "...";
            }

            return s;
        }
        catch
        {
            return "-";
        }
    }
}
