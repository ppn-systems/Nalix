// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Nalix.Common.Concurrency;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Framework.Tests.")]
[assembly: InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Framework.Random;

/// <summary>
/// <para>
/// High-performance server-side pseudo-random generator (NOT CRYPTOGRAPHIC).
/// - Core: xoshiro256++ with per-thread state.
/// - Collision-mitigation: mixes a per-thread 64-bit counter and a per-process 128-bit instance tag.
/// - Supports periodic reseed via TaskManager (no background threads created here).
/// </para>
/// <para>
/// Use cases: load-balancing jitter, randomized scheduling, sampling, non-sensitive IDs.
/// DO NOT use for cryptographic purposes (keys, IVs, tokens).
/// </para>
/// </summary>
[StackTraceHidden]
[DebuggerStepThrough]
[DebuggerDisplay("OsRandom (NOT CSPRNG)")]
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class OsRandom
{
    #region Fields

    private static readonly ulong s_tag0;
    private static readonly ulong s_tag1;
    private static volatile int s_version;      // bump on reseed
    private static readonly ulong[] s_state;     // global base state
    private static readonly byte[] s_instanceTag; // per-process tag (GUID bytes)

    private static volatile IRecurringHandle? s_reseedHandle; // IRecurringHandle 

    // Thread-local state
    [ThreadStatic] private static int t_version;
    [ThreadStatic] private static ulong t_counter;
    [ThreadStatic] private static ulong[]? t_state;

    #endregion Fields

    #region Constructors

    static OsRandom()
    {
        s_state = new ulong[4];
        s_instanceTag = new byte[16];

        // Seed from monotonic/time/process/thread + GUID (no OS RNG).
        // This is not cryptographically strong, but good enough for non-crypto randomness.
        Span<byte> seed = stackalloc byte[32];

        long ticks = DateTime.UtcNow.Ticks;
        long tc64 = Environment.TickCount64;
        int pid = Environment.ProcessId;
        int tid = Environment.CurrentManagedThreadId;

        MemoryMarshal.Write(seed[0..8], in ticks);
        MemoryMarshal.Write(seed[8..16], in tc64);
        MemoryMarshal.Write(seed[16..20], in pid);
        MemoryMarshal.Write(seed[20..24], in tid);

        // Guid-based per-process tag to spread instances on the same host
        byte[] g = Guid.NewGuid().ToByteArray();
        MemoryExtensions.AsSpan(g, 0, 16).CopyTo(s_instanceTag);

        s_tag0 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(MemoryExtensions.AsSpan(s_instanceTag, 0, 8));
        s_tag1 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(MemoryExtensions.AsSpan(s_instanceTag, 8, 8));

        INITIALIZE_STATE(seed);
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Attach a TaskManager to auto-reseed the global state at the specified interval.
    /// Safe to call multiple times; will cancel the previous recurring reseed if any.
    /// </summary>
    /// <remarks>
    /// Recommended interval: 1-5 minutes for long-running servers.
    /// This improves randomness quality by periodically refreshing the internal state
    /// with fresh entropy from system sources. Thread-safe.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Attach()
    {
        // Cancel previous schedule if exists
        IRecurringHandle? old = Interlocked.Exchange(ref s_reseedHandle, null);
        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().CancelRecurring(old?.Name);

        // Schedule new reseed (non-reentrant)
        s_reseedHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
            name: "OsRandom.reseed",
            interval: TimeSpan.FromSeconds(180),
            work: static _ =>
            {
                RESEED_GLOBAL();
                return default;
            },
            options: new RecurringOptions
            {
                NonReentrant = true,
                Jitter = TimeSpan.FromSeconds(54),
                Tag = "random"
            }
        );
    }

    /// <summary>
    /// Detach from TaskManager (stop periodic reseeding).
    /// </summary>
    /// <remarks>
    /// Safe to call even if Attach() was never called or has already been detached.
    /// Thread-safe.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Detach()
    {
        IRecurringHandle? h = Interlocked.Exchange(ref s_reseedHandle, null);
        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().CancelRecurring(h?.Name);
    }

    /// <summary>
    /// Fills the span with pseudo-random bytes (NOT cryptographic).
    /// </summary>
    /// <param name="dst">The span to fill with random bytes.</param>
    /// <remarks>
    /// Thread-safe. Each thread maintains its own local state for lock-free performance.
    /// Automatically reseeds thread-local state when the global state is updated.
    /// WARNING: This is NOT cryptographically secure - use OsCsprng for security-sensitive operations.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    public static void Fill(Span<byte> dst)
    {
        if (dst.Length == 0)
        {
            return;
        }

        ulong[] st = THREAD_STATE();

        if (dst.Length < 8)
        {
            ulong x = NEXT_U64(st);
            for (int i = 0; i < dst.Length; i++)
            {
                dst[i] = (byte)(x & 0xFF);
                x >>= 8;
            }
            return;
        }

        Span<ulong> u64 = MemoryMarshal.Cast<byte, ulong>(dst);
        for (int i = 0; i < u64.Length; i++)
        {
            u64[i] = NEXT_U64(st);
        }

        int rem = dst.Length - (u64.Length * 8);
        if (rem > 0)
        {
            ulong x = NEXT_U64(st);
            for (int i = 0; i < rem; i++)
            {
                dst[dst.Length - rem + i] = (byte)(x & 0xFF);
                x >>= 8;
            }
        }
    }

    #endregion APIs

    #region Privates

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong[] THREAD_STATE()
    {
        ulong[]? st = t_state;
        if (st is not null && t_version == s_version)
        {
            return st;
        }

        ulong base0 = s_state[0];
        ulong base1 = s_state[1];
        ulong base2 = s_state[2];
        ulong base3 = s_state[3];

        ulong tid = (ulong)Environment.CurrentManagedThreadId;
        ulong now = (ulong)DateTime.UtcNow.Ticks;
        ulong tagMix = SPLIT_MIX_64(s_tag0 ^ System.Numerics.BitOperations.RotateLeft(s_tag1, 11));

        ulong[] local = [base0 ^ tid, base1 ^ (tid * 0x9E3779B97F4A7C15UL), base2 ^ now, base3 ^ tagMix];
        for (int i = 0; i < 16; i++)
        {
            _ = XOSHIRO_NEXT(local);
        }

        t_state = local;
        t_version = s_version;
        t_counter = 0;

        return local;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static ulong NEXT_U64(ulong[] st)
    {
        // xoshiro256++ core
        ulong r = XOSHIRO_NEXT(st);

        // Mix per-thread counter and process tag to mitigate repeats
        ulong c = ++t_counter;
        r ^= System.Numerics.BitOperations.RotateLeft(c, 17);
        r ^= ((c >> 3) & 1) == 0 ? s_tag0 : s_tag1; // alternate 0/8 offsets

        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static ulong XOSHIRO_NEXT(ulong[] s)
    {
        ulong result = System.Numerics.BitOperations.RotateLeft(s[0] + s[3], 23) + s[0];
        ulong t = s[1] << 17;

        s[2] ^= s[0];
        s[3] ^= s[1];
        s[1] ^= s[2];
        s[0] ^= s[3];

        s[2] ^= t;
        s[3] = System.Numerics.BitOperations.RotateLeft(s[3], 45);

        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void INITIALIZE_STATE(ReadOnlySpan<byte> seed)
    {
        // Expand 32 bytes into 4x64 via SPLIT_MIX_64 to avoid linearities
        ulong z0 = READ_U64(seed, 0);
        ulong z1 = READ_U64(seed, 8);
        ulong z2 = READ_U64(seed, 16);
        ulong z3 = READ_U64(seed, 24);

        ulong a = SPLIT_MIX_64(z0 ^ 0x9E3779B97F4A7C15UL);
        ulong b = SPLIT_MIX_64(z1 ^ 0xBF58476D1CE4E5B9UL);
        ulong c = SPLIT_MIX_64(z2 ^ 0x94D049BB133111EBUL);
        ulong d = SPLIT_MIX_64(z3 ^ 0xD2B74407B1CE6E93UL);

        s_state[0] = a;
        s_state[1] = b;
        s_state[2] = c;
        s_state[3] = d;

        // burn-in
        for (int i = 0; i < 20; i++)
        {
            _ = XOSHIRO_NEXT(s_state);
        }

        unchecked { s_version++; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RESEED_GLOBAL()
    {
        Span<byte> seed = stackalloc byte[32];

        long ticks = DateTime.UtcNow.Ticks;
        long tc64 = Environment.TickCount64;
        int pid = Environment.ProcessId;

        MemoryMarshal.Write(seed[0..8], in ticks);
        MemoryMarshal.Write(seed[8..16], in tc64);
        MemoryMarshal.Write(seed[16..20], in pid);

        // Fold in the process tag and a moving counter derived from WorkingSet and Stopwatch
        ulong tag0 = READ_U64(s_instanceTag, 0);
        ulong tag1 = READ_U64(s_instanceTag, 8);
        ulong mono = (ulong)Stopwatch.GetTimestamp();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(
            seed[24..32], tag0 ^ System.Numerics.BitOperations.RotateLeft(tag1, 13) ^ mono ^ (ulong)Environment.WorkingSet);

        ulong a = SPLIT_MIX_64(READ_U64(seed, 0) ^ 0x9E3779B97F4A7C15UL);
        ulong b = SPLIT_MIX_64(READ_U64(seed, 8) ^ 0xBF58476D1CE4E5B9UL);
        ulong c = SPLIT_MIX_64(READ_U64(seed, 16) ^ 0x94D049BB133111EBUL);
        ulong d = SPLIT_MIX_64(READ_U64(seed, 24) ^ 0xD2B74407B1CE6E93UL);

        s_state[0] = System.Numerics.BitOperations.RotateLeft(s_state[0], 7) + a;
        s_state[1] ^= b;
        s_state[2] = System.Numerics.BitOperations.RotateLeft(s_state[2] + c, 17);
        s_state[3] ^= System.Numerics.BitOperations.RotateLeft(d, 29);

        for (int i = 0; i < 8; i++)
        {
            _ = XOSHIRO_NEXT(s_state);
        }

        unchecked { s_version++; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static ulong SPLIT_MIX_64(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        ulong x = z;
        x ^= x >> 30; x *= 0xBF58476D1CE4E5B9UL;
        x ^= x >> 27; x *= 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static ulong READ_U64(ReadOnlySpan<byte> s, int offset)
        => System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(s.Slice(offset, 8));

    #endregion Privates
}
