// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Tasks;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Framework.Tasks.Options;

namespace Nalix.Framework.Randomization;

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
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("FastRandom (NOT CSPRNG)")]
public static class FastRandom
{
    #region Fields

    private static volatile System.Int32 s_version;      // bump on reseed
    private static readonly System.UInt64[] s_state;     // global base state
    private static readonly System.Byte[] s_instanceTag; // per-process tag (GUID bytes)
    private static readonly System.Threading.Lock s_lock;

    private static volatile IRecurringHandle? s_reseedHandle; // IRecurringHandle 

    // Thread-local state
    [System.ThreadStatic] private static System.Int32 t_version;
    [System.ThreadStatic] private static System.UInt64 t_counter;
    [System.ThreadStatic] private static System.UInt64[]? t_state;

    #endregion Fields

    #region Constructors

    static FastRandom()
    {
        s_lock = new();
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
        var g = System.Guid.NewGuid().ToByteArray();
        System.MemoryExtensions.AsSpan(g, 0, 16).CopyTo(s_instanceTag);
        System.MemoryExtensions.AsSpan(s_instanceTag, 0, 8).CopyTo(seed[24..32]);

        InitializeState(seed);
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Attach a TaskManager to auto-reseed the global state at the specified interval.
    /// Safe to call multiple times; will cancel the previous recurring reseed if any.
    /// </summary>
    /// <remarks>Recommended interval: 1-5 minutes for long-running servers.</remarks>
    public static void Attach()
    {
        // Cancel previous schedule if exists
        IRecurringHandle? old = System.Threading.Interlocked.Exchange(ref s_reseedHandle, null);
        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().CancelRecurring(old?.Name);

        // Schedule new reseed (non-reentrant)
        s_reseedHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
            name: "FastRandom.reseed",
            interval: System.TimeSpan.FromSeconds(180),
            work: static _ =>
            {
                ReseedGlobal();
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
    public static void Detach()
    {
        IRecurringHandle? h = System.Threading.Interlocked.Exchange(ref s_reseedHandle, null);
        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().CancelRecurring(h?.Name);
    }

    /// <summary>
    /// Fills the span with pseudo-random bytes (NOT cryptographic).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Fill(System.Span<System.Byte> dst)
    {
        if (dst.Length == 0)
        {
            return;
        }

        // Ensure TLS state up-to-date
        var st = GetThreadState();

        // Fast path: write 8 bytes at a time
        System.Span<System.UInt64> u64 = System.Runtime.InteropServices.MemoryMarshal.Cast<System.Byte, System.UInt64>(dst);
        for (System.Int32 i = 0; i < u64.Length; i++)
        {
            u64[i] = NextU64(st);
        }

        // Tail
        System.Int32 rem = dst.Length - (u64.Length * 8);
        if (rem > 0)
        {
            System.UInt64 x = NextU64(st);
            System.Span<System.Byte> tmp = stackalloc System.Byte[8];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(tmp, x);
            tmp[..rem].CopyTo(dst[^rem..]);
        }
    }

    #endregion APIs

    #region Privates

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt64[] GetThreadState()
    {
        var st = t_state;
        if (st is not null && t_version == s_version)
        {
            return st;
        }

        // Recreate TLS state from global base + thread-specific salt + instance tag
        System.UInt64[] local = new System.UInt64[4];
        lock (s_lock)
        {
            local[0] = s_state[0] ^ (System.UInt64)System.Environment.CurrentManagedThreadId;
            local[1] = s_state[1] ^ (System.UInt64)System.Environment.CurrentManagedThreadId * 0x9E3779B97F4A7C15UL;
            local[2] = s_state[2] ^ (System.UInt64)System.DateTime.UtcNow.Ticks;
            local[3] = s_state[3] ^ SplitMix64(ReadU64(s_instanceTag, 0) ^ ReadU64(s_instanceTag, 8));

            // scramble a bit
            for (System.Int32 i = 0; i < 16; i++)
            {
                _ = XoshiroNext(local);
            }
        }

        t_state = local;
        t_version = s_version;
        t_counter = 0;
        return local;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt64 NextU64(System.UInt64[] st)
    {
        // xoshiro256++ core
        System.UInt64 r = XoshiroNext(st);

        // Mix per-thread counter and process tag to mitigate repeats
        System.UInt64 c = ++t_counter;
        r ^= RotateLeft(c, 17);
        r ^= ReadU64(s_instanceTag, (System.Int32)((c >> 3) & 8)); // alternate 0/8 offsets

        return r;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt64 XoshiroNext(System.UInt64[] s)
    {
        System.UInt64 result = RotateLeft(s[0] + s[3], 23) + s[0];
        System.UInt64 t = s[1] << 17;

        s[2] ^= s[0];
        s[3] ^= s[1];
        s[1] ^= s[2];
        s[0] ^= s[3];

        s[2] ^= t;
        s[3] = RotateLeft(s[3], 45);

        return result;
    }

    private static void InitializeState(System.ReadOnlySpan<System.Byte> seed)
    {
        // Expand 32 bytes into 4x64 via SplitMix64 to avoid linearities
        System.UInt64 z0 = ReadU64(seed, 0);
        System.UInt64 z1 = ReadU64(seed, 8);
        System.UInt64 z2 = ReadU64(seed, 16);
        System.UInt64 z3 = ReadU64(seed, 24);

        System.UInt64 a = SplitMix64(z0 ^ 0x9E3779B97F4A7C15UL);
        System.UInt64 b = SplitMix64(z1 ^ 0xBF58476D1CE4E5B9UL);
        System.UInt64 c = SplitMix64(z2 ^ 0x94D049BB133111EBUL);
        System.UInt64 d = SplitMix64(z3 ^ 0xD2B74407B1CE6E93UL);

        lock (s_lock)
        {
            s_state[0] = a;
            s_state[1] = b;
            s_state[2] = c;
            s_state[3] = d;

            // burn-in
            for (System.Int32 i = 0; i < 20; i++)
            {
                _ = XoshiroNext(s_state);
            }

            unchecked { s_version++; }
        }
    }

    private static void ReseedGlobal()
    {
        System.Span<System.Byte> seed = stackalloc System.Byte[32];

        System.Int64 ticks = System.DateTime.UtcNow.Ticks;
        System.Int64 tc64 = System.Environment.TickCount64;
        System.Int32 pid = System.Environment.ProcessId;

        System.Runtime.InteropServices.MemoryMarshal.Write(seed[0..8], in ticks);
        System.Runtime.InteropServices.MemoryMarshal.Write(seed[8..16], in tc64);
        System.Runtime.InteropServices.MemoryMarshal.Write(seed[16..20], in pid);

        // Fold in the process tag and a moving counter derived from WorkingSet and Stopwatch
        System.UInt64 tag0 = ReadU64(s_instanceTag, 0);
        System.UInt64 tag1 = ReadU64(s_instanceTag, 8);
        System.UInt64 mono = (System.UInt64)System.Diagnostics.Stopwatch.GetTimestamp();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(
            seed[24..32], tag0 ^ RotateLeft(tag1, 13) ^ mono ^ (System.UInt64)System.Environment.WorkingSet);

        System.UInt64 a = SplitMix64(ReadU64(seed, 0) ^ 0x9E3779B97F4A7C15UL);
        System.UInt64 b = SplitMix64(ReadU64(seed, 8) ^ 0xBF58476D1CE4E5B9UL);
        System.UInt64 c = SplitMix64(ReadU64(seed, 16) ^ 0x94D049BB133111EBUL);
        System.UInt64 d = SplitMix64(ReadU64(seed, 24) ^ 0xD2B74407B1CE6E93UL);

        lock (s_lock)
        {
            s_state[0] = RotateLeft(s_state[0], 7) + a;
            s_state[1] ^= b;
            s_state[2] = RotateLeft(s_state[2] + c, 17);
            s_state[3] ^= RotateLeft(d, 29);

            for (System.Int32 i = 0; i < 8; i++)
            {
                _ = XoshiroNext(s_state);
            }

            unchecked { s_version++; }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt64 SplitMix64(System.UInt64 z)
    {
        z += 0x9E3779B97F4A7C15UL;
        System.UInt64 x = z;
        x ^= x >> 30; x *= 0xBF58476D1CE4E5B9UL;
        x ^= x >> 27; x *= 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return x;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt64 RotateLeft(System.UInt64 x, System.Int32 k) => (x << k) | (x >> (64 - k));

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.UInt64 ReadU64(System.ReadOnlySpan<System.Byte> s, System.Int32 offset)
        => System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(s.Slice(offset, 8));

    #endregion Privates
}
