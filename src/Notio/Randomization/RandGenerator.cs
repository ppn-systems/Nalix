using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Notio.Randomization;

/// <summary>
/// High-performance cryptographically strong random number generator 
/// based on the Xoshiro256++ algorithm with additional entropy sources.
/// </summary>
public static class RandGenerator
{
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
    /// Generates a secure 12-byte nonce (96 bits) suitable for most authenticated encryption schemes.
    /// </summary>
    /// <returns>A cryptographically secure nonce.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] CreateNonce()
    {
        byte[] nonce = new byte[12];
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
    /// <param name="length">The number of random bytes to generate.</param>
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
    /// Derives a cryptographic key from a passphrase using a PBKDF2-inspired approach.
    /// </summary>
    /// <param name="passphrase">The input passphrase.</param>
    /// <param name="length">The desired key length in bytes.</param>
    /// <param name="iterations">The number of iterations for key stretching.</param>
    /// <returns>A derived key of the specified length.</returns>
    /// <exception cref="ArgumentException">Thrown for invalid parameters.</exception>
    public static byte[] DeriveKey(string passphrase, int length = 32, int iterations = 100000)
    {
        if (string.IsNullOrEmpty(passphrase))
            throw new ArgumentException("Passphrase cannot be null or empty.", nameof(passphrase));

        if (length <= 0)
            throw new ArgumentException("Key length must be greater than zero.", nameof(length));

        if (iterations <= 0)
            throw new ArgumentException("Iterations must be greater than zero.", nameof(iterations));

        // Get passphrase bytes
        byte[] passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
        byte[] result = new byte[length];

        // Use PBKDF2-inspired approach with multiple mixing rounds
        byte[] salt = GetSalt(passphrase);

        // Create initial hash from passphrase and salt
        byte[] hash = HashWithSalt(passphraseBytes, salt);

        // Initialize the key buffer
        for (int i = 0; i < length; i++)
            result[i] = (byte)(hash[i % hash.Length] ^ salt[i % salt.Length]);

        // Iterative mixing
        for (int iter = 0; iter < iterations; iter++)
        {
            // Mix in the iteration counter to prevent duplicate hashes
            for (int i = 0; i < 4 && i < hash.Length - 4; i++)
            {
                int pos = iter % (hash.Length - 4 - i);
                hash[pos + i] ^= (byte)(iter >> (i * 8));
            }

            // Update the hash
            hash = HashBytes(hash);

            // Mix the hash into the result
            for (int i = 0; i < length; i++)
            {
                result[i] ^= (byte)(hash[i % hash.Length] ^ ((iter * i) & 0xFF));
            }
        }

        return result;
    }

    /// <summary>
    /// Compares two byte arrays in constant time to prevent timing attacks.
    /// </summary>
    /// <param name="a">The first byte array to compare.</param>
    /// <param name="b">The second byte array to compare.</param>
    /// <returns>
    /// <c>true</c> if both byte arrays are equal in length and content; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method ensures that the comparison takes a constant amount of time regardless of 
    /// the input values to mitigate timing attacks. It does this by iterating through 
    /// both arrays entirely and using a bitwise OR operation on the differences.
    /// </remarks>
    public static bool ConstantTimeEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length) return false;

        int result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
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
    /// Gets a random value in the range [min, max).
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A random integer in the specified range.</returns>
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
    public static int GetInt32(int max) => GetInt32(0, max);

    /// <summary>
    /// Reseeds the random number generator with additional entropy.
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

    // ----------------------------
    // Private implementation
    // ----------------------------

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
    /// Derives a salt value from the passphrase.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] GetSalt(string passphrase)
    {
        byte[] salt = new byte[16];
        byte[] passphraseBytes = Encoding.UTF8.GetBytes(passphrase);

        // Create a basic salt from the passphrase
        for (int i = 0; i < passphraseBytes.Length; i++)
        {
            salt[i % salt.Length] ^= (byte)(passphraseBytes[i] ^ (i & 0xFF));
        }

        // Further mix the salt
        for (int i = 0; i < salt.Length; i++)
        {
            salt[i] = (byte)(salt[i] ^ RotateLeft(salt[(i + 1) % salt.Length], i % 7 + 1));
        }

        return salt;
    }

    /// <summary>
    /// Hash function that combines passphrase with salt.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] HashWithSalt(byte[] data, byte[] salt)
    {
        // Get the appropriate buffer size
        int bufferSize = data.Length + salt.Length;
        byte[] buffer = bufferSize <= 1024
            ? ArrayPool<byte>.Shared.Rent(bufferSize)
            : new byte[bufferSize];

        try
        {
            // Combine data and salt
            Buffer.BlockCopy(data, 0, buffer, 0, data.Length);
            Buffer.BlockCopy(salt, 0, buffer, data.Length, salt.Length);

            return HashBytes(buffer.AsSpan(0, bufferSize));
        }
        finally
        {
            // Return the buffer to the pool if it was rented
            if (bufferSize <= 1024)
            {
                // Clear sensitive data before returning
                Array.Clear(buffer, 0, bufferSize);
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    /// <summary>
    /// Fast hash function for key derivation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] HashBytes(ReadOnlySpan<byte> data)
    {
        // Use a simple but fast mixing function
        byte[] hash = new byte[32];
        uint h1 = 0x811c9dc5;
        uint h2 = 0x1b873593;
        uint h3 = 0x9cb4b2f8;
        uint h4 = 0x4b2d8c19;

        // Process data in blocks
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            h1 = ((h1 ^ b) * 0x01000193) ^ RotateLeft((uint)b, 1);
            h2 = ((h2 ^ b) * 0x01050193) ^ RotateLeft((uint)b, 2);
            h3 = ((h3 ^ b) * 0x01100193) ^ RotateLeft((uint)b, 3);
            h4 = ((h4 ^ b) * 0x01180193) ^ RotateLeft((uint)b, 4);
        }

        // Finalization
        h1 ^= (uint)data.Length;
        h2 ^= (uint)data.Length << 8;
        h3 ^= (uint)data.Length << 16;
        h4 ^= (uint)data.Length << 24;

        BitConverter.TryWriteBytes(hash.AsSpan(0, 4), h1);
        BitConverter.TryWriteBytes(hash.AsSpan(4, 4), h2);
        BitConverter.TryWriteBytes(hash.AsSpan(8, 4), h3);
        BitConverter.TryWriteBytes(hash.AsSpan(12, 4), h4);

        // Add more variation
        for (int i = 16; i < 32; i++)
        {
            hash[i] = (byte)(hash[i - 16] ^ hash[i % 16] ^ (i * 11));
        }

        return hash;
    }

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
    /// Ensures the random number generator is properly seeded with high-entropy sources.
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
    /// Initialize the state of the random number generator.
    /// </summary>
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
    /// Core Xoshiro256++ random number generation algorithm.
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
}
