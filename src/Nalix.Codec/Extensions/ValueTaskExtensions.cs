// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.DataFrames;
using Nalix.Codec.Pooling;

namespace Nalix.Codec.Extensions;

/// <summary>
/// Provides extension methods for ValueTask to support optimized state machine allocations.
/// </summary>
public static class ValueTaskExtensions
{
    /// <summary>
    /// Checks the task completion synchronously and returns immediately if successful.
    /// Otherwise, allocates an async state machine to await it. Automatically disposes the lease.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask DisposeOnCompletionAsync<TPacket>(this ValueTask pending, PacketScope<TPacket> lease)
        where TPacket : PacketBase<TPacket>, IPacketStaticOpcode, new()
    {
        if (pending.IsCompletedSuccessfully)
        {
#pragma warning disable CA1849
            pending.GetAwaiter().GetResult();
#pragma warning restore CA1849
            lease.Dispose();
            return default;
        }

        return AwaitAsync(pending, lease);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask AwaitAsync(ValueTask task, PacketScope<TPacket> scope)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            finally
            {
                scope.Dispose();
            }
        }
    }
}
