// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.IO;
using Nalix.Abstractions;
using Nalix.Environment.IO;

namespace Nalix.Framework.Extensions;

/// <summary>
/// Provides extension methods and utilities for diagnostic reports.
/// </summary>
public static class ReportExtensions
{
    #region Dashboard Utilities

    /// <summary>
    /// Formats a large number into a compact string (e.g., 1.2k, 3.5M).
    /// </summary>
    internal static string FormatCompact(this long value)
    {
        if (value < 1000)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        if (value < 1000000)
        {
            return FormatDecimal(value / 1000.0, "k");
        }

        if (value < 1000000000)
        {
            return FormatDecimal(value / 1000000.0, "M");
        }

        return FormatDecimal(value / 1000000000.0, "G");

        static string FormatDecimal(double val, string unit)
        {
            if (Math.Abs(val - Math.Round(val)) < 0.0001)
            {
                return val.ToString("F0", CultureInfo.InvariantCulture) + unit;
            }
            return val.ToString("F1", CultureInfo.InvariantCulture) + unit;
        }
    }

    /// <summary>
    /// Formats a large number into a compact string (e.g., 1.2k, 3.5M).
    /// </summary>
    internal static string FormatCompact(this int value) => FormatCompact((long)value);

    /// <summary>
    /// Groups two values into a single compact string (e.g., "15 / 1024" or "1.2k / 5k").
    /// </summary>
    internal static string FormatGroup(long current, long total, bool compact = false)
    {
        string c = compact ? FormatCompact(current) : current.ToString(CultureInfo.InvariantCulture);
        string t = compact ? FormatCompact(total) : total.ToString(CultureInfo.InvariantCulture);
        return $"{c} / {t}";
    }

    /// <summary>
    /// Formats a type name by shortening it if it exceeds a maximum length.
    /// </summary>
    internal static string FormatTypeName(string name, int maxLength = 24)
    {
        if (name.Length <= maxLength)
        {
            return name.PadRight(maxLength);
        }

        return $"{name.AsSpan(0, maxLength - 3)}...".PadRight(maxLength);
    }

    /// <summary>
    /// Formats a TimeSpan into a compact human-readable string (e.g., 1.5s, 10m).
    /// </summary>
    internal static string FormatTimeSpan(this TimeSpan value)
    {
        double ms = value.TotalMilliseconds;
        if (ms < 1000)
        {
            return $"{ms:F0}ms";
        }

        double sec = value.TotalSeconds;
        if (sec < 60)
        {
            return FormatDecimal(sec, "s");
        }

        double min = value.TotalMinutes;
        if (min < 60)
        {
            return FormatDecimal(min, "m");
        }

        double hours = value.TotalHours;
        if (hours < 24)
        {
            return FormatDecimal(hours, "h");
        }

        return FormatDecimal(value.TotalDays, "d");

        static string FormatDecimal(double val, string unit)
        {
            return Math.Abs(val % 1) < 0.001
                ? $"{val:F0}{unit}"
                : $"{val:F1}{unit}";
        }
    }

    #endregion Dashboard Utilities
    private static readonly string s_reportDir;

    static ReportExtensions()
    {
        try
        {
            s_reportDir = Path.GetFullPath(Path
                              .Combine(Directories.DataDirectory, "reports"));

            _ = Directory.CreateDirectory(s_reportDir);
        }
        catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            s_reportDir = Path.Combine(AppContext.BaseDirectory, "reports");
        }
    }

    /// <summary>
    /// Saves the generated report of the manager to a file inside DataDirectory/reports.
    /// </summary>
    /// <param name="this">The reportable manager.</param>
    /// <param name="prefix">Optional filename prefix, e.g. "buffer" or "object".</param>
    /// <returns>The full path of the saved report file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="this"/> is null.</exception>
    /// <remarks>
    /// File-system errors are allowed to propagate to the caller.
    /// </remarks>
    public static string SaveReportToFile(this IReportable @this, string prefix = "null")
    {
        ArgumentNullException.ThrowIfNull(@this, nameof(@this));
        string safePrefix = SANITIZE_FILE_NAME_SEGMENT(prefix?.ToLowerInvariant() ?? "null");
        string report = @this.GenerateReport();
        string filePath = Path.Combine(s_reportDir, $"{safePrefix}-report-{DateTime.UtcNow:yyyyMMdd-HHmm}.txt");

        _ = Directory.CreateDirectory(s_reportDir);
        File.WriteAllText(filePath, report);

        return filePath;
    }

    private static string SANITIZE_FILE_NAME_SEGMENT(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "null";
        }

        // Use a broader set of invalid characters for cross-platform consistency.
        // Windows is more restrictive than Linux, so we adopt a "least Abstractions denominator" approach.
        char[] invalidChars = ['*', '?', ':', '"', '<', '>', '|', '/', '\\', '\0'];
        char[] result = value.ToCharArray();
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = Array.IndexOf(invalidChars, result[i]) >= 0 ? '_' : result[i];
        }

        return new string(result);
    }
}
