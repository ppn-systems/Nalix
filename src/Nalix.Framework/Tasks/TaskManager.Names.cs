// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Tasks;

/// <summary>
/// Provides a standardized naming scheme for groups, workers, and recurring jobs.
/// <para>
/// Conventions:
/// - Groups use path-style with '/' separators (e.g., "net/tcp/5720").
/// - Workers use dot-style with '.' separators (e.g., "tcp.accept.5720.0").
/// - All names are lowercase; only [A-Za-z0-9-_.] allowed after <see cref="SanitizeToken(System.String)"/>.
/// </para>
/// </summary>
/// <remarks>
/// This is a general-purpose library. Specific domains (TCP, limiter, etc.)
/// should build on top of these helpers rather than hardcoding names here.
/// </remarks>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
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
        public const System.String Process = "proc";

        /// <summary>
        /// Tag for tasks that synchronize time or state.
        /// </summary>
        public const System.String Worker = "worker";

        /// <summary>
        /// Tag for tasks that accept connections or requests.
        /// </summary>
        public const System.String Accept = "accept";

        /// <summary>
        /// Tag for tasks that perform cleanup operations.
        /// </summary>
        public const System.String Cleanup = "cleanup";

        /// <summary>
        /// Tag for tasks related to service operations.
        /// </summary>
        public const System.String Service = "service";

        /// <summary>
        /// Tag for tasks that dispatch work or messages.
        /// </summary>
        public const System.String Dispatch = "dispatch";
    }

    /// <summary>
    /// Group name builder (path-style).
    /// </summary>
    public static class Groups
    {
        /// <summary>
        /// Build a generic group path from parts, e.g. <c>Build("net","tcp","5720") = "net/tcp/5720"</c>.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public static System.String Build(params System.String[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return "-";
            }

            for (System.Int32 i = 0; i < parts.Length; i++)
            {
                parts[i] = SanitizeToken(parts[i]);
            }

            return System.String.Join('/', parts);
        }

        /// <summary>
        /// Optimized overload for 3-part group paths (no params array).
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Roslynator", "RCS1267:Use string interpolation instead of 'string.Concat'", Justification = "<Pending>")]
        public static System.String Build(System.String p1, System.String p2, System.String p3)
        {
            p1 = SanitizeToken(p1);
            p2 = SanitizeToken(p2);
            p3 = SanitizeToken(p3);

            // "net/tcp/5720"
            return System.String.Concat(
                System.String.Concat(p1, "/"),
                    System.String.Concat(p2,
                        System.String.Concat("/", p3)));
        }

        /// <summary>
        /// Optimized overload for 4-part group paths (no params array).
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Roslynator", "RCS1267:Use string interpolation instead of 'string.Concat'", Justification = "<Pending>")]
        public static System.String Build(System.String p1, System.String p2, System.String p3, System.String p4)
        {
            p1 = SanitizeToken(p1);
            p2 = SanitizeToken(p2);
            p3 = SanitizeToken(p3);
            p4 = SanitizeToken(p4);

            // "net/tcp/5720/proc"
            return System.String.Concat(
                System.String.Concat(p1, "/"),
                System.String.Concat(
                    p2, System.String.Concat(
                        "/", System.String.Concat(
                            p3, System.String.Concat("/", p4)))));
        }
    }

    /// <summary>
    /// Worker name builder (dot-style).
    /// </summary>
    public static class Workers
    {
        /// <summary>
        /// Build a generic worker id, e.g. <c>Build("tcp","accept","5720","0") = "tcp.accept.5720.0"</c>.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public static System.String Build(params System.String[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return "-";
            }

            for (System.Int32 i = 0; i < parts.Length; i++)
            {
                parts[i] = SanitizeToken(parts[i]);
            }

            return System.String.Join('.', parts);
        }

        /// <summary>
        /// Optimized overload for 3-part worker ids (no params array).
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Roslynator", "RCS1267:Use string interpolation instead of 'string.Concat'", Justification = "<Pending>")]
        public static System.String Build(System.String p1, System.String p2, System.String p3)
        {
            p1 = SanitizeToken(p1);
            p2 = SanitizeToken(p2);
            p3 = SanitizeToken(p3);

            // "udp.proc.7777"
            return System.String.Concat(
                System.String.Concat(p1, "."),
                    System.String.Concat(p2,
                        System.String.Concat(".", p3)));
        }

        /// <summary>
        /// Optimized overload for 4-part worker ids (no params array).
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Roslynator", "RCS1267:Use string interpolation instead of 'string.Concat'", Justification = "<Pending>")]
        public static System.String Build(System.String p1, System.String p2, System.String p3, System.String p4)
        {
            p1 = SanitizeToken(p1);
            p2 = SanitizeToken(p2);
            p3 = SanitizeToken(p3);
            p4 = SanitizeToken(p4);

            // "tcp.accept.5720.0"
            return System.String.Concat(
                System.String.Concat(p1, "."),
                System.String.Concat(
                    p2, System.String.Concat(
                        ".", System.String.Concat(
                            p3, System.String.Concat(".", p4)))));
        }

        /// <summary>
        /// Build a worker id with a time-based component, e.g. "timesync.10ms".
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public static System.String Periodic(System.String prefix, System.TimeSpan period)
        {
            System.Double ms = period.TotalMilliseconds;
            System.String token = ms % 1 == 0
                ? ((System.Int64)ms).ToString(System.Globalization.CultureInfo.InvariantCulture) + "ms"
                : ms.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "ms";

            return $"{SanitizeToken(prefix)}.{token}";
        }
    }

    /// <summary>
    /// Recurring job name builder (dot-style).
    /// </summary>
    public static class Recurring
    {
        /// <summary>
        /// Build a recurring job id with a hex instance key, e.g. "cleanup.00BC614E".
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public static System.String CleanupJobId(System.String prefix, System.Int32 instanceKey) => $"{SanitizeToken(prefix)}.{Tags.Cleanup}.{instanceKey:X8}";
    }

    /// <summary>
    /// Sanitizes an arbitrary string into a safe token for task names.
    /// Allows letters, digits, '-', '_', '.', replaces others with '_'.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [System.Runtime.CompilerServices.SkipLocalsInit]
    public static System.String SanitizeToken(System.String s)
    {
        if (System.String.IsNullOrEmpty(s))
        {
            return "-";
        }

        System.ReadOnlySpan<System.Char> input = System.MemoryExtensions.AsSpan(s);
        System.Span<System.Char> buf = input.Length <= 256
            ? stackalloc System.Char[input.Length]
            : new System.Char[input.Length];

        System.Int32 k = 0;
        for (System.Int32 i = 0; i < input.Length; i++)
        {
            System.Char c = input[i];
            buf[k++] = System.Char.IsLetterOrDigit(c) || c is '-' or '_' or '.'
                ? c : '_';
        }

        return new System.String(buf[..k]);
    }
}
