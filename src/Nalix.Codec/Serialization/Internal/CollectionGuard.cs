// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Codec.Internal;
using Nalix.Environment.Memory;

namespace Nalix.Codec.Serialization.Internal;

internal static class CollectionGuard
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void EnsureRead(ref DataReader reader, int count, int bytesPerElement = 1)
    {
        if (count < 0 || count > SerializationStaticOptions.Instance.MaxArrayLength)
        {
            Throw.LengthOutOfRange();
        }

        long minimumBytes = (long)count * bytesPerElement;
        if (minimumBytes > int.MaxValue)
        {
            Throw.Overflow();
        }

        if (reader.BytesRemaining < minimumBytes)
        {
            Throw.EndOfStream();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int EnsureCan(ref DataReader reader, int count, int elementSize)
    {
        if (count < 0 || count > SerializationStaticOptions.Instance.MaxArrayLength)
        {
            Throw.LengthOutOfRange();
        }

        long totalBytesLong = (long)count * elementSize;
        if (totalBytesLong > int.MaxValue)
        {
            Throw.Overflow();
        }

        int totalBytes = (int)totalBytesLong;
        if (reader.BytesRemaining < totalBytes)
        {
            Throw.EndOfStream();
        }

        return totalBytes;
    }
}
