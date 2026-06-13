// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Nalix.Environment.Memory;

namespace Nalix.Codec.Serialization.Internal;

/// <summary>
/// Provides diagnostic logging for serialization failures, keeping the hot paths lean.
/// </summary>
internal static class SerializationDiagnostics
{
    /// <summary>
    /// Poisons the reader and emits a diagnostic event containing the failure reason and location.
    /// Marked with NoInlining to prevent bloat in the calling hot paths.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Poison(ref DataReader reader, string reason, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        reader.IsFailed = true;

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Serialization.Poisoned))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Serialization.Poisoned, new
            {
                Reason = reason,
                Member = memberName,
                File = filePath,
                Line = lineNumber
            });
        }
    }
}
