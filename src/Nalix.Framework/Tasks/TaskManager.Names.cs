// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Nalix.Framework.Tasks;

/// <summary>
/// Provides a standardized naming scheme for groups, workers, and recurring jobs.
/// <para>
/// Conventions:
/// - Groups use path-style with '/' separators (e.g., "net/tcp/5720").
/// - Workers use dot-style with '.' separators (e.g., "tcp.accept.5720.0").
/// - All names are lowercase; only [A-Za-z0-9-_.] allowed after <see cref="SanitizeToken(string)"/>.
/// </para>
/// </summary>
/// <remarks>
/// This is a general-purpose library. Specific domains (TCP, limiter, etc.)
/// should build on top of these helpers rather than hardcoding names here.
/// </remarks>
[StackTraceHidden]
[DebuggerStepThrough]
[ExcludeFromCodeCoverage]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TaskNaming
{
    /// <summary>
    /// Canonical short tags that appear in task names.
    /// Applications may extend this with domain-specific tags.
    /// </summary>
    public static class Tags
    {
        /// <summary>
        /// Tag for tasks that process data or requests.
        /// </summary>
        public const string Process = "proc";

        /// <summary>
        /// Tag for tasks that synchronize time or state.
        /// </summary>
        public const string Worker = "worker";

        /// <summary>
        /// Tag for tasks that accept connections or requests.
        /// </summary>
        public const string Accept = "accept";

        /// <summary>
        /// Tag for tasks that perform cleanup operations.
        /// </summary>
        public const string Cleanup = "cleanup";

        /// <summary>
        /// Tag for tasks related to service operations.
        /// </summary>
        public const string Service = "service";

        /// <summary>
        /// Tag for tasks that dispatch work or messages.
        /// </summary>
        public const string Dispatch = "dispatch";
    }

    /// <summary>
    /// Recurring job name builder (dot-style).
    /// </summary>
    public static class Recurring
    {
        /// <summary>
        /// Build a recurring job id with a hex instance key, e.g. "cleanup.00BC614E".
        /// </summary>
        /// <remarks>
        /// Invalid characters in <paramref name="prefix"/> are replaced by <c>_</c> via <see cref="SanitizeToken(string)"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static string CleanupJobId(string prefix, int instanceKey) => $"{SanitizeToken(prefix)}.{Tags.Cleanup}.{instanceKey:X8}";
    }

    /// <summary>
    /// Sanitizes an arbitrary string into a safe token for task names.
    /// Allows letters, digits, '-', '_', '.', replaces others with '_'.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [SkipLocalsInit]
    public static string SanitizeToken(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "-";
        }

        ReadOnlySpan<char> input = MemoryExtensions.AsSpan(s);
        Span<char> buf = input.Length <= 256
            ? stackalloc char[input.Length]
            : new char[input.Length];

        int k = 0;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            buf[k++] = char.IsLetterOrDigit(c) || c is '-' or '_' or '.'
                ? c : '_';
        }

        return new string(buf[..k]);
    }
}
