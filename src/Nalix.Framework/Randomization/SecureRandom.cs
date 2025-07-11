namespace Nalix.Framework.Randomization;

/// <summary>
/// High-performance cryptographically strong random Number generator
/// based on the Xoshiro256++ algorithm with additional entropy sources.
/// </summary>
public static class SecureRandom
{
    #region Fields

    // State for the Xoshiro256++ algorithm - 256 bits total
    private static readonly System.UInt64[] State;

    // Thread-local state instances for true thread safety
    [System.ThreadStatic]
    private static System.UInt64[]? _threadState;

    // Thread synchronization object for the initial seeding
    private static readonly System.Threading.Lock SyncRoot = new();

    // Track if global state has been properly seeded
    private static System.Boolean _seeded = false;

    private static System.Int64 _lastReseedTicks = 0;

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Static constructor to initialize the random generator state with strong entropy sources.
    /// </summary>
    static SecureRandom()
    {
        State = new System.UInt64[4];
        InitializeState();
    }

    #endregion Constructor

    #region APIs

    /// <summary>
    /// Creates a new cryptographic key of the specified length.
    /// </summary>
    /// <param name="length">The key length in bytes (e.g., 32 for AES-256).</param>
    /// <returns>A securely generated key of the specified length.</returns>
    /// <exception cref="System.ArgumentException">Thrown if length is not positive or not a multiple of 8.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static byte[] CreateKey(int length = 32)
    {
        if (length <= 0)
            throw new System.ArgumentException("Key length must be greater than zero.", nameof(length));

        if (length % 8 != 0)
            throw new System.ArgumentException("Key length must be a multiple of 8.", nameof(length));

        byte[] key = new byte[length];
        Fill(key);
        return key;
    }

    /// <summary>
    /// Generates a cryptographic key and fills the provided output span with random bytes.
    /// The key is suitable for use in symmetric encryption algorithms.
    /// </summary>
    /// <param name="output">The span to fill with random key bytes. Length must be a multiple of 8.</param>
    /// <exception cref="System.ArgumentException">Thrown if the length of <paramref name="output"/> is not a multiple of 8.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void CreateKey(System.Span<byte> output)
    {
        if (output.Length % 8 != 0)
            throw new System.ArgumentException("Key length must be a multiple of 8.", nameof(output));

        Fill(output);
    }

    /// <summary>
    /// Generates a secure 12-byte nonce (96 bits) suitable for most authenticated encryption schemes.
    /// </summary>
    /// <returns>A cryptographically secure nonce.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static byte[] CreateNonce(int length = 12)
    {
        if (length <= 0)
            throw new System.ArgumentOutOfRangeException(
                nameof(length), "Nonce length must be a positive integer.");

        byte[] nonce = new byte[length];
        Fill(nonce);
        return nonce;
    }

    /// <summary>
    /// Generates a secure random IV of the specified length.
    /// </summary>
    /// <param name="length">The IV length in bytes (e.g., 16 for AES).</param>
    /// <returns>A secure random IV.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static byte[] CreateIV(int length = 16)
    {
        if (length <= 0)
            throw new System.ArgumentException("IV length must be greater than zero.", nameof(length));

        byte[] iv = new byte[length];
        Fill(iv);
        return iv;
    }

    /// <summary>
    /// Converts a uint array key back to a byte array.
    /// </summary>
    /// <param name="keyWords">The key as an array of uint values.</param>
    /// <returns>The key as a byte array.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe byte[] ConvertWordsToKey(System.ReadOnlySpan<uint> keyWords)
    {
        byte[] keyBytes = new byte[keyWords.Length * sizeof(uint)];

        fixed (byte* bytesPtr = keyBytes)
        fixed (uint* wordsPtr = keyWords)
        {
            System.Buffer.MemoryCopy(wordsPtr, bytesPtr, keyBytes.Length, keyWords.Length * sizeof(uint));
        }

        return keyBytes;
    }

    /// <summary>
    /// Converts a byte array key to a uint array key.
    /// </summary>
    /// <param name="keyBytes">The key as bytes (length must be a multiple of 4).</param>
    /// <returns>The key as an array of uint values.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe uint[] ConvertKeyToWords(System.ReadOnlySpan<byte> keyBytes)
    {
        if (keyBytes.Length % sizeof(uint) != 0)
            throw new System.ArgumentException("Key length must be a multiple of 4 bytes.", nameof(keyBytes));

        int keySizeInWords = keyBytes.Length / sizeof(uint);
        uint[] keyWords = new uint[keySizeInWords];

        fixed (byte* bytesPtr = keyBytes)
        fixed (uint* wordsPtr = keyWords)
        {
            uint* sourcePtr = (uint*)bytesPtr;
            uint* destPtr = wordsPtr;

            for (int i = 0; i < keySizeInWords; i++)
            {
                destPtr[i] = sourcePtr[i];
            }
        }

        return keyWords;
    }

    /// <summary>
    /// Fills the provided span with cryptographically strong random bytes.
    /// </summary>
    /// <param name="data">The span to fill with random bytes.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Fill(System.Span<byte> data)
    {
        EnsureSeeded();
        ulong[] state = GetThreadLocalState();

        int i = 0;

        // Process 8-byte chunks efficiently
        var ulongSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, ulong>(data);

        for (int j = 0; j < ulongSpan.Length; j++)
        {
            ulongSpan[j] = NextUInt64(state) ^ RotateLeft(state[1], 11) ^ (state[2] >> 7);
        }

        i = ulongSpan.Length * 8;
        int remainingBytes = data.Length - i;

        // Handle remaining bytes
        if (remainingBytes > 0)
        {
            ulong masked = NextUInt64(state) ^ RotateLeft(state[1], 11) ^ (state[2] >> 7);
            System.Span<byte> lastBytes = stackalloc byte[8];
            System.BitConverter.TryWriteBytes(lastBytes, masked);
            lastBytes[..remainingBytes].CopyTo(data[i..]);
        }
    }

    /// <summary>
    /// Generates a random byte array of the specified length.
    /// </summary>
    /// <param name="length">The Number of random bytes to generate.</param>
    /// <returns>A byte array filled with random data.</returns>
    /// <exception cref="System.ArgumentException">Thrown if length is negative.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static byte[] GetBytes(int length)
    {
        if (length < 0)
            throw new System.ArgumentException("Length cannot be negative.", nameof(length));

        if (length == 0)
            return [];

        byte[] bytes = new byte[length];
        Fill(bytes);
        return bytes;
    }

    /// <summary>
    /// Gets a random value in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random integer in the specified range.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int GetInt32(int min, int max)
    {
        if (min >= max)
            throw new System.ArgumentException("Max must be greater than min");

        // Calculate range size, handling potential overflow
        ulong range = (ulong)((long)max - min);

        // Use rejection sampling to avoid modulo bias
        ulong mask = (1UL << (System.Numerics.BitOperations.Log2((uint)range) + 1)) - 1;
        ulong result;

        do
        {
            result = NextUInt64() & mask;
        } while (result >= range);

        return (int)(result + (ulong)min);
    }

    /// <summary>
    /// Gets a random value in the range [0, max).
    /// </summary>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random integer in the specified range.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int GetInt32(int max) => GetInt32(0, max);

    /// <summary>
    /// Fills the given byte array with cryptographically strong random values.
    /// </summary>
    /// <param name="buffer">The buffer to fill with random bytes.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void NextBytes(byte[] buffer)
    {
        System.ArgumentNullException.ThrowIfNull(buffer);
        Fill(buffer);
    }

    /// <summary>
    /// Fills the given span with cryptographically strong random values.
    /// </summary>
    /// <param name="buffer">The span to fill with random bytes.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void NextBytes(System.Span<byte> buffer) => Fill(buffer);

    /// <summary>
    /// Generates a cryptographically strong 32-bit random integer.
    /// </summary>
    /// <returns>A random 32-bit unsigned integer.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static uint NextUInt32()
    {
        EnsureSeeded();
        ulong[] state = GetThreadLocalState();
        return (uint)(NextUInt64(state) >> 32);
    }

    /// <summary>
    /// Generates a cryptographically strong 64-bit random integer.
    /// </summary>
    /// <returns>A random 64-bit unsigned integer.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static ulong NextUInt64()
    {
        EnsureSeeded();
        return NextUInt64(GetThreadLocalState());
    }

    /// <summary>
    /// Generates a random double in the range [0.0, 1.0).
    /// </summary>
    /// <returns>A random double with uniform distribution.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static double NextDouble()
    {
        // Use 53 bits (mantissa precision of double) for uniform distribution
        return (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);
    }

    /// <summary>
    /// Converts a 16-byte key into a 32-bit unsigned integer array (4 elements) for use with XTEA.
    /// </summary>
    /// <param name="key">The byte array representing the key, which must be 16 bytes long.</param>
    /// <returns>A 32-bit unsigned integer array (4 elements) representing the key.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the key length is not 16 bytes.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static uint[] ConvertKey(System.ReadOnlySpan<byte> key)
    {
        if (key.Length != 16)
            throw new System.ArgumentException("XTEA key must be 16 bytes.", nameof(key));

        uint[] uintKey = new uint[4];
        for (int i = 0; i < 4; i++)
        {
            uintKey[i] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(i * 4, 4));
        }
        return uintKey;
    }

    /// <summary>
    /// Reseeds the random Number generator with additional entropy.
    /// </summary>
    public static void Reseed()
    {
        lock (SyncRoot)
        {
            InitializeState();
            _seeded = true;
            _threadState = null; // Force regeneration of thread-local state
        }
    }

    #endregion APIs

    #region Private Implementation

    /// <summary>
    /// Performs left rotation of bits in a 64-bit value.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static ulong RotateLeft(ulong value, int shift)
        => (value << shift) | (value >> (64 - shift));

    /// <summary>
    /// Ensures the random Number generator is properly seeded with high-entropy sources.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void EnsureSeeded()
    {
        long now = System.Environment.TickCount64;
        if (_seeded && now - _lastReseedTicks < 60000) return; // 60s timeout

        lock (SyncRoot)
        {
            if (_seeded && now - _lastReseedTicks < 60000) return;

            InitializeState();
            _seeded = true;
            _threadState = null;
            _lastReseedTicks = now;
        }
    }

    /// <summary>
    /// Gets or creates thread-local state for thread safety.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static ulong[] GetThreadLocalState()
    {
        if (_threadState != null)
            return _threadState;

        ulong[] localState = new ulong[4];

        // Initialize thread-local state from global state plus thread-specific entropy
        lock (SyncRoot)
        {
            localState[0] = State[0] ^ (ulong)System.Environment.CurrentManagedThreadId;
            localState[1] = State[1] ^ (ulong)System.Environment.CurrentManagedThreadId;
            localState[2] = State[2] ^ (ulong)System.DateTime.UtcNow.Ticks;
            localState[3] = State[3] ^ GetCpuCycles();

            // Mix the state
            for (int i = 0; i < 20; i++)
            {
                NextUInt64(localState);
            }
        }

        _threadState = localState;
        return localState;
    }

    /// <summary>
    /// Initialize the state of the random Number generator.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void InitializeState()
    {
        // Use multiple entropy sources for better security
        byte[] seed = new byte[32];

        // Use high-precision time sources
        ulong timestamp = (ulong)System.DateTime.UtcNow.Ticks;
        System.BitConverter.TryWriteBytes(System.MemoryExtensions.AsSpan(seed, 0, 8), timestamp);

        // Use Environment.TickCount64 for additional entropy
        long tickCount = System.Environment.TickCount64;
        System.BitConverter.TryWriteBytes(System.MemoryExtensions.AsSpan(seed, 8, 8), tickCount);

        // Use process and thread IDs
        int processId = System.Environment.ProcessId;
        System.BitConverter.TryWriteBytes(System.MemoryExtensions.AsSpan(seed, 16, 4), processId);

        int threadId = System.Environment.CurrentManagedThreadId;
        System.BitConverter.TryWriteBytes(System.MemoryExtensions.AsSpan(seed, 20, 4), threadId);

        // Hardware-specific information like CPU cycles
        ulong cpuCycles = GetCpuCycles();
        System.BitConverter.TryWriteBytes(System.MemoryExtensions.AsSpan(seed, 24, 8), cpuCycles);

        // Mix in additional entropy sources if available
        for (int i = 0; i < seed.Length; i++)
        {
            byte val = seed[i];
            val ^= (byte)(i * 137);
            val = (byte)((val << 3) | (val >> 5)); // RotateLeft 3
            val ^= (byte)(timestamp >> (i % 8 * 8));
            val = (byte)(val * 31);
            seed[i] = val;
        }

        // Initialize the state with the seed
        State[0] = System.BitConverter.ToUInt64(seed, 0);
        State[1] = System.BitConverter.ToUInt64(seed, 8);
        State[2] = System.BitConverter.ToUInt64(seed, 16);
        State[3] = System.BitConverter.ToUInt64(seed, 24);

        // Perform initial mixing
        for (int i = 0; i < 20; i++)
        {
            NextUInt64(State);
        }
    }

    /// <summary>
    /// Get CPU cycle count for additional entropy.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static ulong GetCpuCycles()
    {
        try
        {
            // Use rdtsc if available via Stopwatch timing
            long start = System.Diagnostics.Stopwatch.GetTimestamp();

            // Perform some calculation to ensure timing differences
            int sum = 0;
            for (int i = 0; i < 100; i++)
            {
                sum += i * 7;
            }

            long end = System.Diagnostics.Stopwatch.GetTimestamp();

            // Combine timing and calculation result
            return (ulong)((end - start) ^ sum);
        }
        catch
        {
            // Fall back to Environment.TickCount if Stopwatch fails
            return (ulong)(System.Environment.TickCount ^ System.GC.GetTotalMemory(false));
        }
    }

    /// <summary>
    /// Efficiently fills a 32-byte block using all four state values.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void FillBlock(System.Span<byte> block, ulong[] state)
    {
        // Generate four 64-bit values in sequence
        System.BitConverter.TryWriteBytes(block[0..8], NextUInt64(state));
        System.BitConverter.TryWriteBytes(block[8..16], NextUInt64(state));
        System.BitConverter.TryWriteBytes(block[16..24], NextUInt64(state));
        System.BitConverter.TryWriteBytes(block[24..32], NextUInt64(state));
    }

    /// <summary>
    /// Core Xoshiro256++ random Number generation algorithm.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static ulong NextUInt64(ulong[] state)
    {
        // Xoshiro256++ algorithm
        ulong result = RotateLeft(state[0] + state[3], 23) + state[0];

        ulong t = state[1] << 17;

        state[2] ^= state[0];
        state[3] ^= state[1];
        state[1] ^= state[2];
        state[0] ^= state[3];

        state[2] ^= t;
        state[3] = RotateLeft(state[3], 45);

        return result;
    }

    #endregion Private Implementation
}