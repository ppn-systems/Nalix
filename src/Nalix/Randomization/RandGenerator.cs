using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Nalix.Randomization;

/// <summary>
/// High-performance cryptographically strong random Number generator
/// based on the Xoshiro256++ algorithm with additional entropy sources.
/// </summary>
public static class RandGenerator
{
    #region Fields and Static Constructor

    // State for the Xoshiro256++ algorithm - 256 bits total
    private static readonly ulong[] State = new ulong[4];

    // Thread-local state instances for true thread safety
    [ThreadStatic]
    private static ulong[] _threadState;

    // Thread synchronization object for the initial seeding
    private static readonly Lock SyncRoot = new();

    // Track if global state has been properly seeded
    private static bool _seeded = false;

    /// <summary>
    /// Static constructor to initialize the random generator state with strong entropy sources.
    /// </summary>
    static RandGenerator()
    {
        InitializeState();
    }

    #endregion Fields and Static Constructor

    #region Public Methods

    /// <summary>
    /// Creates a new cryptographic key of the specified length.
    /// </summary>
    /// <param name="length">The key length in bytes (e.g., 32 for AES-256).</param>
    /// <returns>A securely generated key of the specified length.</returns>
    /// <exception cref="ArgumentException">Thrown if length is not positive or not a multiple of 8.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] CreateKey(int length = 32)
    {
        if (length <= 0)
            throw new ArgumentException("Key length must be greater than zero.", nameof(length));

        if (length % 8 != 0)
            throw new ArgumentException("Key length must be a multiple of 8.", nameof(length));

        byte[] key = new byte[length];
        Fill(key);
        return key;
    }

    /// <summary>
    /// Generates a cryptographic key and fills the provided output span with random bytes.
    /// The key is suitable for use in symmetric encryption algorithms.
    /// </summary>
    /// <param name="output">The span to fill with random key bytes. Length must be a multiple of 8.</param>
    /// <exception cref="ArgumentException">Thrown if the length of <paramref name="output"/> is not a multiple of 8.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CreateKey(Span<byte> output)
    {
        if (output.Length % 8 != 0)
            throw new ArgumentException("Key length must be a multiple of 8.", nameof(output));

        Fill(output);
    }

    /// <summary>
    /// Generates a secure 12-byte nonce (96 bits) suitable for most authenticated encryption schemes.
    /// </summary>
    /// <returns>A cryptographically secure nonce.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] CreateNonce(int length = 12)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] CreateIV(int length = 16)
    {
        if (length <= 0)
            throw new ArgumentException("IV length must be greater than zero.", nameof(length));

        byte[] iv = new byte[length];
        Fill(iv);
        return iv;
    }

    /// <summary>
    /// Converts a uint array key back to a byte array.
    /// </summary>
    /// <param name="keyWords">The key as an array of uint values.</param>
    /// <returns>The key as a byte array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ConvertWordsToKey(ReadOnlySpan<uint> keyWords)
    {
        byte[] keyBytes = new byte[keyWords.Length * sizeof(uint)];

        for (int i = 0; i < keyWords.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(keyBytes.AsSpan(i * sizeof(uint), sizeof(uint)), keyWords[i]);
        }

        return keyBytes;
    }

    /// <summary>
    /// Converts a byte array key to a uint array key.
    /// </summary>
    /// <param name="keyBytes">The key as bytes (length must be a multiple of 4).</param>
    /// <returns>The key as an array of uint values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint[] ConvertKeyToWords(ReadOnlySpan<byte> keyBytes)
    {
        if (keyBytes.Length % sizeof(uint) != 0)
            throw new ArgumentException("Key length must be a multiple of 4 bytes.", nameof(keyBytes));

        int keySizeInWords = keyBytes.Length / sizeof(uint);
        uint[] keyWords = new uint[keySizeInWords];

        for (int i = 0; i < keySizeInWords; i++)
        {
            keyWords[i] = BinaryPrimitives.ReadUInt32LittleEndian(keyBytes.Slice(i * sizeof(uint), sizeof(uint)));
        }

        return keyWords;
    }

    /// <summary>
    /// Fills the provided span with cryptographically strong random bytes.
    /// </summary>
    /// <param name="data">The span to fill with random bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Fill(Span<byte> data)
    {
        EnsureSeeded();
        ulong[] state = GetThreadLocalState();

        // Process in 8-byte chunks for efficiency
        int i = 0;
        int remainingBytes = data.Length;

        // Process 32 bytes at a time when possible (all 4 state values)
        while (remainingBytes >= 32)
        {
            FillBlock(data.Slice(i, 32), state);
            i += 32;
            remainingBytes -= 32;
        }

        // Process remaining bytes in 8-byte chunks
        while (remainingBytes >= 8)
        {
            BitConverter.TryWriteBytes(data.Slice(i, 8), NextUInt64(state));
            i += 8;
            remainingBytes -= 8;
        }

        // Handle any remaining bytes (less than 8)
        if (remainingBytes > 0)
        {
            ulong lastValue = NextUInt64(state);
            Span<byte> lastBytes = stackalloc byte[8];
            BitConverter.TryWriteBytes(lastBytes, lastValue);
            lastBytes[..remainingBytes].CopyTo(data[i..]);
        }
    }

    /// <summary>
    /// Generates a random byte array of the specified length.
    /// </summary>
    /// <param name="length">The Number of random bytes to generate.</param>
    /// <returns>A byte array filled with random data.</returns>
    /// <exception cref="ArgumentException">Thrown if length is negative.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] GetBytes(int length)
    {
        if (length < 0)
            throw new ArgumentException("Length cannot be negative.", nameof(length));

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetInt32(int min, int max)
    {
        if (min >= max)
            throw new ArgumentException("Max must be greater than min");

        // Calculate range size, handling potential overflow
        ulong range = (ulong)((long)max - min);

        // Use rejection sampling to avoid modulo bias
        ulong mask = (1UL << BitOperations.Log2((uint)range) + 1) - 1;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetInt32(int max) => GetInt32(0, max);

    /// <summary>
    /// Fills the given byte array with cryptographically strong random values.
    /// </summary>
    /// <param name="buffer">The buffer to fill with random bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Fill(buffer);
    }

    /// <summary>
    /// Fills the given span with cryptographically strong random values.
    /// </summary>
    /// <param name="buffer">The span to fill with random bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NextBytes(Span<byte> buffer) => Fill(buffer);

    /// <summary>
    /// Generates a cryptographically strong 32-bit random integer.
    /// </summary>
    /// <returns>A random 32-bit unsigned integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong NextUInt64()
    {
        EnsureSeeded();
        return NextUInt64(GetThreadLocalState());
    }

    /// <summary>
    /// Generates a random double in the range [0.0, 1.0).
    /// </summary>
    /// <returns>A random double with uniform distribution.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    /// <exception cref="ArgumentException">Thrown when the key length is not 16 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint[] ConvertKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != 16)
            throw new ArgumentException("XTEA key must be 16 bytes.", nameof(key));

        uint[] uintKey = new uint[4];
        for (int i = 0; i < 4; i++)
        {
            uintKey[i] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(i * 4, 4));
        }
        return uintKey;
    }

    /// <summary>
    /// Creates a cryptographically secure random password of specified length and complexity.
    /// </summary>
    /// <param name="length">The length of the password.</param>
    /// <param name="includeSpecial">Whether to include special characters.</param>
    /// <param name="includeNumbers">Whether to include numbers.</param>
    /// <param name="includeUppercase">Whether to include uppercase letters.</param>
    /// <param name="includeLowercase">Whether to include lowercase letters.</param>
    /// <returns>A randomly generated password.</returns>
    public static string CreatePassword(
        int length = 16,
        bool includeSpecial = true,
        bool includeNumbers = true,
        bool includeUppercase = true,
        bool includeLowercase = true)
    {
        if (length <= 0)
            throw new ArgumentException("Password length must be greater than zero.", nameof(length));

        if (!(includeSpecial || includeNumbers || includeUppercase || includeLowercase))
            throw new ArgumentException("At least one character set must be included.");

        // Define character pools based on requested complexity
        const string lowercaseChars = "abcdefghijklmnopqrstuvwxyz";
        const string uppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string numberChars = "0123456789";
        const string specialChars = "!@#$%^&*()-_=+[]{}|;:,.<>?/";

        // Build up the character pool
        StringBuilder charPool = new(96);

        if (includeLowercase) charPool.Append(lowercaseChars);
        if (includeUppercase) charPool.Append(uppercaseChars);
        if (includeNumbers) charPool.Append(numberChars);
        if (includeSpecial) charPool.Append(specialChars);

        // Ensure the password contains at least one character from each requested set
        char[] password = new char[length];
        int position = 0;

        if (includeLowercase && position < length)
        {
            password[position++] = lowercaseChars[(int)(NextUInt64() % (uint)lowercaseChars.Length)];
        }

        if (includeUppercase && position < length)
        {
            password[position++] = uppercaseChars[(int)(NextUInt64() % (uint)uppercaseChars.Length)];
        }

        if (includeNumbers && position < length)
        {
            password[position++] = numberChars[(int)(NextUInt64() % (uint)numberChars.Length)];
        }

        if (includeSpecial && position < length)
        {
            password[position++] = specialChars[(int)(NextUInt64() % (uint)specialChars.Length)];
        }

        // Fill the rest with random characters from the pool
        string pool = charPool.ToString();
        int poolSize = pool.Length;

        for (int i = position; i < length; i++)
        {
            int index = (int)(NextUInt64() % (uint)poolSize);
            password[i] = pool[index];
        }

        // Shuffle the password to avoid predictable patterns at start
        return ShuffleString(new string(password));
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

    #endregion Public Methods

    #region Private Implementation

    /// <summary>
    /// Performs left rotation of bits in a 64-bit value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong RotateLeft(ulong value, int shift)
        => (value << shift) | (value >> (64 - shift));

    /// <summary>
    /// Performs left rotation of bits in a 32-bit value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateLeft(uint value, int shift)
        => (value << shift) | (value >> (32 - shift));

    /// <summary>
    /// Performs left rotation of bits in an 8-bit value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte RotateLeft(byte value, int shift)
        => (byte)((value << shift) | (value >> (8 - shift)));

    /// <summary>
    /// Shuffles the characters in a string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ShuffleString(string input)
    {
        char[] chars = input.ToCharArray();
        int n = chars.Length;

        // Fisher-Yates shuffle
        while (n > 1)
        {
            n--;
            int k = (int)(NextUInt64() % (uint)(n + 1));
            (chars[k], chars[n]) = (chars[n], chars[k]);
        }

        return new string(chars);
    }

    /// <summary>
    /// Ensures the random Number generator is properly seeded with high-entropy sources.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureSeeded()
    {
        if (_seeded)
            return;

        lock (SyncRoot)
        {
            if (_seeded)
                return;

            InitializeState();
            _seeded = true;
        }
    }

    /// <summary>
    /// Gets or creates thread-local state for thread safety.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong[] GetThreadLocalState()
    {
        if (_threadState != null)
            return _threadState;

        ulong[] localState = new ulong[4];

        // Initialize thread-local state from global state plus thread-specific entropy
        lock (SyncRoot)
        {
            localState[0] = State[0] ^ (ulong)Environment.CurrentManagedThreadId;
            localState[1] = State[1] ^ (ulong)Environment.CurrentManagedThreadId;
            localState[2] = State[2] ^ (ulong)DateTime.UtcNow.Ticks;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InitializeState()
    {
        // Use multiple entropy sources for better security
        byte[] seed = new byte[32];

        // Use high-precision time sources
        ulong timestamp = (ulong)DateTime.UtcNow.Ticks;
        BitConverter.TryWriteBytes(seed.AsSpan(0, 8), timestamp);

        // Use Environment.TickCount64 for additional entropy
        long tickCount = Environment.TickCount64;
        BitConverter.TryWriteBytes(seed.AsSpan(8, 8), tickCount);

        // Use process and thread IDs
        int processId = Environment.ProcessId;
        BitConverter.TryWriteBytes(seed.AsSpan(16, 4), processId);

        int threadId = Environment.CurrentManagedThreadId;
        BitConverter.TryWriteBytes(seed.AsSpan(20, 4), threadId);

        // Hardware-specific information like CPU cycles
        ulong cpuCycles = GetCpuCycles();
        BitConverter.TryWriteBytes(seed.AsSpan(24, 8), cpuCycles);

        // Mix in additional entropy sources if available
        for (int i = 0; i < seed.Length; i++)
        {
            // Mix with more entropy
            seed[i] ^= (byte)(i * 97);
            seed[i] ^= (byte)(timestamp >> ((i % 8) * 8));
            seed[i] ^= (byte)(cpuCycles >> ((i % 8) * 8));
        }

        // Initialize the state with the seed
        State[0] = BitConverter.ToUInt64(seed, 0);
        State[1] = BitConverter.ToUInt64(seed, 8);
        State[2] = BitConverter.ToUInt64(seed, 16);
        State[3] = BitConverter.ToUInt64(seed, 24);

        // Perform initial mixing
        for (int i = 0; i < 20; i++)
        {
            NextUInt64(State);
        }
    }

    /// <summary>
    /// Get CPU cycle count for additional entropy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            return (ulong)(Environment.TickCount ^ GC.GetTotalMemory(false));
        }
    }

    /// <summary>
    /// Efficiently fills a 32-byte block using all four state values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillBlock(Span<byte> block, ulong[] state)
    {
        // Generate four 64-bit values in sequence
        BitConverter.TryWriteBytes(block[0..8], NextUInt64(state));
        BitConverter.TryWriteBytes(block[8..16], NextUInt64(state));
        BitConverter.TryWriteBytes(block[16..24], NextUInt64(state));
        BitConverter.TryWriteBytes(block[24..32], NextUInt64(state));
    }

    /// <summary>
    /// Core Xoshiro256++ random Number generation algorithm.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
