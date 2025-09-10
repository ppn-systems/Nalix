// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;

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
        return $"[Data]: {FormatExtendedData(extendedData)}" +
               $"[Source]: {sourceName ?? "NONE"}{System.Environment.NewLine}" +
               $"[Caller]: {callerMemberName} in {callerFilePath} at line {callerLineNumber}{System.Environment.NewLine}" +
               $"[Message]: {message}{System.Environment.NewLine}";
    }

    private static System.String FormatExtendedData(System.Object? extendedData)
    {
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
            const System.Int32 MaxLen = 200;
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
