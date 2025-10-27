// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

using Nalix.Common.Diagnostics;

namespace Nalix.Logging.Extensions;

public static partial class NLogixFx
{
    private const string Sep = "================================================================================";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PUBLISH_LOG_ENTRY(
        LogLevel level,
        string message,
        string? sourceName,
        object? extendedData,
        string callerMemberName,
        string callerFilePath,
        int callerLineNumber)
    {
        if (!(level > MinimumLevel))
        {
            return;
        }

        string fullMessage = FORMAT_LOG_MESSAGE(
            message, sourceName, extendedData,
            callerMemberName, callerFilePath, callerLineNumber);

        Publisher.Publish(new LogEntry(level, EventId.Empty, fullMessage, null));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string FORMAT_LOG_MESSAGE(
        string message,
        string? sourceName,
        object? extendedData,
        string callerMemberName,
        string callerFilePath, int callerLineNumber)
    {
        return
            $"{Sep}\n" +
            $"Source     : {sourceName ?? "NONE"}\n" +
            $"Caller     : {callerMemberName} ({callerFilePath}:{callerLineNumber})\n" +
            $"Data       : {FORMAT_EXTENDED_DATA(extendedData)}\n" +
            $"Message    : {message}\n" +
            $"{Sep}\n";
    }

    private static string FORMAT_EXTENDED_DATA(object? extendedData)
    {
        const int MaxLen = 200;

        if (extendedData is null)
        {
            return "-";
        }

        try
        {
            if (extendedData is Exception ex)
            {
                string m = ex.Message ?? "";
                return $"{ex.GetType().Name}: {m}";
            }

            string s = extendedData.ToString() ?? "-";
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
