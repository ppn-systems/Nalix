// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Concurrency;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Framework.Random.Core;

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
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.DebuggerDisplay("OsRandom (NOT CSPRNG)")]
internal static class OsRandom
{
    #region Fields

    private static readonly System.UInt64 s_tag0;
    private static readonly System.UInt64 s_tag1;
    private static volatile System.Int32 s_version;      // bump on reseed
    private static readonly System.UInt64[] s_state;     // global base state
    private static readonly System.Byte[] s_instanceTag; // per-process tag (GUID bytes)

    private static volatile IRecurringHandle? s_reseedHandle; // IRecurringHandle 

    // Thread-local state
    [System.ThreadStatic] private static System.Int32 t_version;
    [System.ThreadStatic] private static System.UInt64 t_counter;
    [System.ThreadStatic] private static System.UInt64[]? t_state;

    #endregion Fields

    #region Constructors

    static OsRandom()
    {
        s_state = new System.UInt64[4];
        s_instanceTag = new System.Byte[16];

        // Seed from monotonic/time/process/thread + GUID (no OS RNG).
        // This is not cryptographically strong, but good enough for non-crypto randomness.
        System.Span<System.Byte> seed = stackalloc System.Byte[32];

        System.Int64 ticks = System.DateTime.UtcNow.Ticks;
        System.Int64 tc64 = System.Environment.TickCount64;
        System.Int32 pid = System.Environment.ProcessId;
        System.Int32 tid = System.Environment.CurrentManagedThreadId;

        System.Runtime.InteropServices.MemoryMarshal.Write(seed[0..8], in ticks);
        System.Runtime.InteropServices.MemoryMarshal.Write(seed[8..16], in tc64);
        System.Runtime.InteropServices.MemoryMarshal.Write(seed[16..20], in pid);
        System.Runtime.InteropServices.MemoryMarshal.Write(seed[20..24], in tid);

        // Guid-based per-process tag to spread instances on the same host
        System.Byte[] g = System.Guid.NewGuid().ToByteArray();
        System.MemoryExtensions.AsSpan(g, 0, 16).CopyTo(s_instanceTag);

        s_tag0 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(System.MemoryExtensions.AsSpan(s_instanceTag, 0, 8));
        s_tag1 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(System.MemoryExtensions.AsSpan(s_instanceTag, 8, 8));

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void Attach()
    {
        // Cancel previous schedule if exists
        IRecurringHandle? old = System.Threading.Interlocked.Exchange(ref s_reseedHandle, null);
        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().CancelRecurring(old?.Name);

        // Schedule new reseed (non-reentrant)
        s_reseedHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
            name: "OsRandom.reseed",
            interval: System.TimeSpan.FromSeconds(180),
            work: static _ =>
            {
                RESEED_GLOBAL();
                return default;
            },
            options: new RecurringOptions
            {
                NonReentrant = true,
                Jitter = System.TimeSpan.FromSeconds(54),
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void Detach()
    {
        IRecurringHandle? h = System.Threading.Interlocked.Exchange(ref s_reseedHandle, null);
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void Fill([System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> dst)
    {
        if (dst.Length == 0)
        {
            return;
        }

        System.UInt64[] st = THREAD_STATE();

        if (dst.Length < 8)
        {
            System.UInt64 x = NEXT_U64(st);
            for (System.Int32 i = 0; i < dst.Length; i++)
            {
                dst[i] = (System.Byte)(x & 0xFF);
                x >>= 8;
            }
            return;
        }

        System.Span<System.UInt64> u64 = System.Runtime.InteropServices.MemoryMarshal.Cast<System.Byte, System.UInt64>(dst);
        for (System.Int32 i = 0; i < u64.Length; i++)
        {
            u64[i] = NEXT_U64(st);
        }

        System.Int32 rem = dst.Length - (u64.Length * 8);
        if (rem > 0)
        {
            System.UInt64 x = NEXT_U64(st);
            for (System.Int32 i = 0; i < rem; i++)
            {
                dst[dst.Length - rem + i] = (System.Byte)(x & 0xFF);
                x >>= 8;
            }
        }
    }

    #endregion APIs

    #region Privates

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private static System.UInt64[] THREAD_STATE()
    {
        var st = t_state;
        if (st is not null && t_version == s_version)
        {
            return st;
        }

        System.UInt64 base0 = s_state[0];
        System.UInt64 base1 = s_state[1];
        System.UInt64 base2 = s_state[2];
        System.UInt64 base3 = s_state[3];

        System.UInt64 tid = (System.UInt64)System.Environment.CurrentManagedThreadId;
        System.UInt64 now = (System.UInt64)System.DateTime.UtcNow.Ticks;
        System.UInt64 tagMix = SPLIT_MIX_64(s_tag0 ^ System.Numerics.BitOperations.RotateLeft(s_tag1, 11));

        System.UInt64[] local = [base0 ^ tid, base1 ^ (tid * 0x9E3779B97F4A7C15UL), base2 ^ now, base3 ^ tagMix];
        for (System.Int32 i = 0; i < 16; i++)
        {
            _ = XOSHIRO_NEXT(local);
        }

        t_state = local;
        t_version = s_version;
        t_counter = 0;

        return local;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.UInt64 NEXT_U64(System.UInt64[] st)
    {
        // xoshiro256++ core
        System.UInt64 r = XOSHIRO_NEXT(st);

        // Mix per-thread counter and process tag to mitigate repeats
        System.UInt64 c = ++t_counter;
        r ^= System.Numerics.BitOperations.RotateLeft(c, 17);
        r ^= ((c >> 3) & 1) == 0 ? s_tag0 : s_tag1; // alternate 0/8 offsets

        return r;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.UInt64 XOSHIRO_NEXT(System.UInt64[] s)
    {
        System.UInt64 result = System.Numerics.BitOperations.RotateLeft(s[0] + s[3], 23) + s[0];
        System.UInt64 t = s[1] << 17;

        s[2] ^= s[0];
        s[3] ^= s[1];
        s[1] ^= s[2];
        s[0] ^= s[3];

        s[2] ^= t;
        s[3] = System.Numerics.BitOperations.RotateLeft(s[3], 45);

        return result;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void INITIALIZE_STATE(System.ReadOnlySpan<System.Byte> seed)
    {
        // Expand 32 bytes into 4x64 via SPLIT_MIX_64 to avoid linearities
        System.UInt64 z0 = READ_U64(seed, 0);
        System.UInt64 z1 = READ_U64(seed, 8);
        System.UInt64 z2 = READ_U64(seed, 16);
        System.UInt64 z3 = READ_U64(seed, 24);

        System.UInt64 a = SPLIT_MIX_64(z0 ^ 0x9E3779B97F4A7C15UL);
        System.UInt64 b = SPLIT_MIX_64(z1 ^ 0xBF58476D1CE4E5B9UL);
        System.UInt64 c = SPLIT_MIX_64(z2 ^ 0x94D049BB133111EBUL);
        System.UInt64 d = SPLIT_MIX_64(z3 ^ 0xD2B74407B1CE6E93UL);

        s_state[0] = a;
        s_state[1] = b;
        s_state[2] = c;
        s_state[3] = d;

        // burn-in
        for (System.Int32 i = 0; i < 20; i++)
        {
            _ = XOSHIRO_NEXT(s_state);
        }

        unchecked { s_version++; }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void RESEED_GLOBAL()
    {
        System.Span<System.Byte> seed = stackalloc System.Byte[32];

        System.Int64 ticks = System.DateTime.UtcNow.Ticks;
        System.Int64 tc64 = System.Environment.TickCount64;
        System.Int32 pid = System.Environment.ProcessId;

        System.Runtime.InteropServices.MemoryMarshal.Write(seed[0..8], in ticks);
        System.Runtime.InteropServices.MemoryMarshal.Write(seed[8..16], in tc64);
        System.Runtime.InteropServices.MemoryMarshal.Write(seed[16..20], in pid);

        // Fold in the process tag and a moving counter derived from WorkingSet and Stopwatch
        System.UInt64 tag0 = READ_U64(s_instanceTag, 0);
        System.UInt64 tag1 = READ_U64(s_instanceTag, 8);
        System.UInt64 mono = (System.UInt64)System.Diagnostics.Stopwatch.GetTimestamp();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(
            seed[24..32], tag0 ^ System.Numerics.BitOperations.RotateLeft(tag1, 13) ^ mono ^ (System.UInt64)System.Environment.WorkingSet);

        System.UInt64 a = SPLIT_MIX_64(READ_U64(seed, 0) ^ 0x9E3779B97F4A7C15UL);
        System.UInt64 b = SPLIT_MIX_64(READ_U64(seed, 8) ^ 0xBF58476D1CE4E5B9UL);
        System.UInt64 c = SPLIT_MIX_64(READ_U64(seed, 16) ^ 0x94D049BB133111EBUL);
        System.UInt64 d = SPLIT_MIX_64(READ_U64(seed, 24) ^ 0xD2B74407B1CE6E93UL);

        s_state[0] = System.Numerics.BitOperations.RotateLeft(s_state[0], 7) + a;
        s_state[1] ^= b;
        s_state[2] = System.Numerics.BitOperations.RotateLeft(s_state[2] + c, 17);
        s_state[3] ^= System.Numerics.BitOperations.RotateLeft(d, 29);

        for (System.Int32 i = 0; i < 8; i++)
        {
            _ = XOSHIRO_NEXT(s_state);
        }

        unchecked { s_version++; }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.UInt64 SPLIT_MIX_64(System.UInt64 z)
    {
        z += 0x9E3779B97F4A7C15UL;
        System.UInt64 x = z;
        x ^= x >> 30; x *= 0xBF58476D1CE4E5B9UL;
        x ^= x >> 27; x *= 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return x;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.UInt64 READ_U64(System.ReadOnlySpan<System.Byte> s, System.Int32 offset)
        => System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(s.Slice(offset, 8));

    #endregion Privates
}
