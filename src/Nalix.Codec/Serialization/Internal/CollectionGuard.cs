// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Environment.Memory;

namespace Nalix.Codec.Serialization.Internal;

internal static class CollectionGuard
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool TryEnsureRead(ref DataReader reader, int count, int bytesPerElement = 1)
    {
        if (count < 0 || count > SerializationStaticOptions.Instance.MaxArrayLength)
        {
            SerializationDiagnostics.Poison(ref reader, "Out of bounds read");
            return false;
        }

        long minimumBytes = (long)count * bytesPerElement;
        if (minimumBytes > int.MaxValue)
        {
            SerializationDiagnostics.Poison(ref reader, "Out of bounds read");
            return false;
        }

        if (reader.BytesRemaining < minimumBytes)
        {
            SerializationDiagnostics.Poison(ref reader, "Out of bounds read");
            return false;
        }

        return true;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool TryEnsureCan(ref DataReader reader, int count, int elementSize, out int totalBytes)
    {
        if (count < 0 || count > SerializationStaticOptions.Instance.MaxArrayLength)
        {
            totalBytes = 0;
            SerializationDiagnostics.Poison(ref reader, "Out of bounds read");
            return false;
        }

        long totalBytesLong = (long)count * elementSize;
        if (totalBytesLong > int.MaxValue)
        {
            totalBytes = 0;
            SerializationDiagnostics.Poison(ref reader, "Out of bounds read");
            return false;
        }

        totalBytes = (int)totalBytesLong;
        if (reader.BytesRemaining < totalBytes)
        {
            SerializationDiagnostics.Poison(ref reader, "Out of bounds read");
            return false;
        }

        return true;
    }
}
