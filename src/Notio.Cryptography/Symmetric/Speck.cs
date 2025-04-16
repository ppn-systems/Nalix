using Notio.Cryptography.Utilities;
using Notio.Randomization;
using System;
using System.Runtime.CompilerServices;

namespace Notio.Cryptography.Symmetric;

/// <summary>
/// Implementation of the Speck lightweight block cipher developed by the NSA.
/// This implementation does not rely on system cryptography libraries for the core algorithm,
/// but uses secure random Number generation for IV when needed.
/// </summary>
public static class Speck
{
    #region Constants

    // Speck configuration constants
    private const uint ALPHA = 8; // Rotation constant alpha
    private const uint BETA = 3;  // Rotation constant beta

    #endregion

    #region Public API

    /// <summary>
    /// Encrypts data using the Speck64/128 variant (64-bit block with 128-bit key).
    /// </summary>
    /// <param name="plaintext">The plaintext to encrypt (must be exactly 8 bytes).</param>
    /// <param name="key">The encryption key (must be exactly 16 bytes).</param>
    /// <returns>The encrypted ciphertext (8 bytes).</returns>
    public static byte[] Encrypt(byte[] plaintext, byte[] key)
    {
        if (plaintext is not { Length: 8 })
            throw new ArgumentException(
                "Plaintext must be exactly 8 bytes for Speck64/128",
                nameof(plaintext));

        if (key is not { Length: 16 })
            throw new ArgumentException("Key must be exactly 16 bytes for Speck64/128", nameof(key));

        // Extract two 32-bit blocks from plaintext
        uint x = BitwiseUtils.U8To32Little(plaintext, 0);
        uint y = BitwiseUtils.U8To32Little(plaintext, 4);

        // Generate subkeys from the main key
        uint[] subkeys = GenerateSubkeys64_128(key);

        // Apply encryption rounds
        (x, y) = EncryptBlock(x, y, subkeys);

        // Prepare output
        byte[] ciphertext = new byte[8];
        BitwiseUtils.ToBytes(ciphertext, x, 0);
        BitwiseUtils.ToBytes(ciphertext, y, 4);

        return ciphertext;
    }

    /// <summary>
    /// Encrypts data using the Speck64/128 variant (64-bit block with 128-bit key).
    /// </summary>
    /// <param name="plaintext">The decrypted plaintext (8 bytes).</param>
    /// <param name="key">The decryption key (must be exactly 16 bytes).</param>
    /// <param name="ciphertext">The ciphertext to decrypt (must be exactly 8 bytes).</param>
    /// <exception cref="ArgumentException"></exception>
    public static void Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> key, Span<byte> ciphertext)
    {
        if (plaintext.Length != 8)
            throw new ArgumentException("Plaintext must be exactly 8 bytes for Speck64/128.", nameof(plaintext));

        if (key.Length != 16)
            throw new ArgumentException("Key must be exactly 16 bytes for Speck64/128.", nameof(key));

        uint x = BitwiseUtils.U8To32Little(plaintext, 0);
        uint y = BitwiseUtils.U8To32Little(plaintext, 4);

        uint[] subkeys = GenerateSubkeys64_128(key.ToArray());
        (x, y) = EncryptBlock(x, y, subkeys);

        BitwiseUtils.ToBytes(ciphertext, x, 0);
        BitwiseUtils.ToBytes(ciphertext, y, 4);
    }

    /// <summary>
    /// Decrypts data using the Speck64/128 variant (64-bit block with 128-bit key).
    /// </summary>
    /// <param name="ciphertext">The ciphertext to decrypt (must be exactly 8 bytes).</param>
    /// <param name="key">The decryption key (must be exactly 16 bytes).</param>
    /// <returns>The decrypted plaintext (8 bytes).</returns>
    public static byte[] Decrypt(byte[] ciphertext, byte[] key)
    {
        if (ciphertext is not { Length: 8 })
            throw new ArgumentException(
                "Ciphertext must be exactly 8 bytes for Speck64/128",
                nameof(ciphertext));

        if (key is not { Length: 16 })
            throw new ArgumentException("Key must be exactly 16 bytes for Speck64/128", nameof(key));

        // Extract two 32-bit blocks from ciphertext
        uint x = BitwiseUtils.U8To32Little(ciphertext, 0);
        uint y = BitwiseUtils.U8To32Little(ciphertext, 4);

        // Generate subkeys from the main key
        uint[] subkeys = GenerateSubkeys64_128(key);

        // Apply decryption rounds (in reverse)
        (x, y) = DecryptBlock(x, y, subkeys);

        // Prepare output
        byte[] plaintext = new byte[8];
        BitwiseUtils.ToBytes(plaintext, x, 0);
        BitwiseUtils.ToBytes(plaintext, y, 4);

        return plaintext;
    }

    /// <summary>
    /// Decrypts data using the Speck64/128 variant (64-bit block with 128-bit key).
    /// </summary>
    /// <param name="ciphertext">The ciphertext to decrypt (must be exactly 8 bytes).</param>
    /// <param name="key">The decryption key (must be exactly 16 bytes).</param>
    /// <param name="plaintext">The decrypted plaintext (8 bytes).</param>
    /// <exception cref="ArgumentException"></exception>
    public static void Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> key, Span<byte> plaintext)
    {
        if (ciphertext.Length != 8)
            throw new ArgumentException("Ciphertext must be exactly 8 bytes for Speck64/128.", nameof(ciphertext));

        if (key.Length != 16)
            throw new ArgumentException("Key must be exactly 16 bytes for Speck64/128.", nameof(key));

        // Extract two 32-bit blocks from ciphertext
        uint x = BitwiseUtils.U8To32Little(ciphertext, 0);
        uint y = BitwiseUtils.U8To32Little(ciphertext, 4);

        // Generate subkeys from the main key
        uint[] subkeys = GenerateSubkeys64_128(key.ToArray());

        // Apply decryption rounds (in reverse)
        (x, y) = DecryptBlock(x, y, subkeys);

        // Prepare output
        BitwiseUtils.ToBytes(plaintext, x, 0);
        BitwiseUtils.ToBytes(plaintext, y, 4);
    }

    /// <summary>
    /// Speck in CBC mode.
    /// </summary>
    public static class CBC
    {
        /// <summary>
        /// Encrypts data using CBC mode with the Speck64/128 variant.
        /// </summary>
        /// <param name="plaintext">The plaintext to encrypt (length must be multiple of 8 bytes).</param>
        /// <param name="key">The encryption key (must be exactly 16 bytes).</param>
        /// <param name="iv">The initialization vector (must be exactly 8 bytes). If null, a random IV will be generated.</param>
        /// <returns>The encrypted ciphertext with IV prepended (IV + ciphertext).</returns>
        public static byte[] Encrypt(byte[] plaintext, byte[] key, byte[] iv = null)
        {
            ArgumentNullException.ThrowIfNull(plaintext);

            if (plaintext.Length % 8 != 0)
                throw new ArgumentException(
                    "Plaintext length must be a multiple of 8 bytes for CBC mode",
                    nameof(plaintext));

            if (key is not { Length: 16 })
                throw new ArgumentException(
                    "Key must be exactly 16 bytes for Speck64/128", nameof(key));

            // Generate IV if not provided
            if (iv == null)
            {
                iv = new byte[8];
                RandGenerator.Fill(iv);
            }
            else if (iv.Length != 8)
            {
                throw new ArgumentException("IV must be exactly 8 bytes for Speck64/128", nameof(iv));
            }

            // Generate subkeys
            uint[] subkeys = GenerateSubkeys64_128(key);

            // Prepare output (IV + ciphertext)
            byte[] output = new byte[iv.Length + plaintext.Length];
            Array.Copy(iv, 0, output, 0, iv.Length);

            // Previous block for CBC chaining (initially the IV)
            byte[] previousBlock = (byte[])iv.Clone();

            // Process each block
            for (int i = 0; i < plaintext.Length; i += 8)
            {
                // XOR with previous ciphertext block or IV
                byte[] currentBlock = new byte[8];
                for (int j = 0; j < 8; j++)
                {
                    currentBlock[j] = (byte)(plaintext[i + j] ^ previousBlock[j]);
                }

                // Extract words
                uint x = BitwiseUtils.U8To32Little(currentBlock, 0);
                uint y = BitwiseUtils.U8To32Little(currentBlock, 4);

                // Encrypt block
                (x, y) = EncryptBlock(x, y, subkeys);

                // Store result
                byte[] encryptedBlock = new byte[8];
                BitwiseUtils.ToBytes(encryptedBlock, x, 0);
                BitwiseUtils.ToBytes(encryptedBlock, y, 4);

                // Copy to output
                Array.Copy(encryptedBlock, 0, output, iv.Length + i, 8);

                // Update previous block for next iteration
                previousBlock = encryptedBlock;
            }

            return output;
        }

        /// <summary>
        /// Encrypts data using CBC mode with the Speck64/128 variant.
        /// </summary>
        /// <param name="plaintext">The plaintext to encrypt (length must be multiple of 8 bytes).</param>
        /// <param name="key">The encryption key (must be exactly 16 bytes).</param>
        /// <param name="iv">The initialization vector (must be exactly 8 bytes). If null, a random IV will be generated.</param>
        /// <param name="ciphertext">The output ciphertext with IV prepended (IV + ciphertext).</param>
        /// <exception cref="ArgumentException"></exception>
        public static void Encrypt(
            ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> key,
            Span<byte> ciphertext, ReadOnlySpan<byte> iv = default)
        {
            if (plaintext.Length % 8 != 0)
                throw new ArgumentException(
                    "Plaintext length must be a multiple of 8 bytes for CBC mode",
                    nameof(plaintext));

            if (key.Length != 16)
                throw new ArgumentException(
                    "Key must be exactly 16 bytes for Speck64/128", nameof(key));

            // Generate IV if not provided
            byte[] ivArray = iv.IsEmpty ? new byte[8] : iv.ToArray();
            if (iv.IsEmpty)
            {
                RandGenerator.Fill(ivArray);
            }
            else if (ivArray.Length != 8)
            {
                throw new ArgumentException("IV must be exactly 8 bytes for Speck64/128", nameof(iv));
            }

            // Generate subkeys
            uint[] subkeys = GenerateSubkeys64_128(key.ToArray());

            // Prepare output (IV + ciphertext)
            ivArray.CopyTo(ciphertext[..8]);

            // Previous block for CBC chaining (initially the IV)
            Span<byte> previousBlock = ivArray;

            // Process each block
            for (int i = 0; i < plaintext.Length; i += 8)
            {
                Span<byte> currentBlock = new byte[8];
                for (int j = 0; j < 8; j++)
                {
                    currentBlock[j] = (byte)(plaintext[i + j] ^ previousBlock[j]);
                }

                // Extract words
                uint x = BitwiseUtils.U8To32Little(currentBlock, 0);
                uint y = BitwiseUtils.U8To32Little(currentBlock, 4);

                // Encrypt block
                (x, y) = EncryptBlock(x, y, subkeys);

                // Store result in the ciphertext
                BitwiseUtils.ToBytes(ciphertext[(8 + i)..], x, 0);
                BitwiseUtils.ToBytes(ciphertext[(8 + i)..], y, 4);

                // Update previous block for next iteration
                previousBlock = ciphertext.Slice(8 + i, 8);
            }
        }

        /// <summary>
        /// Decrypts data using CBC mode with the Speck64/128 variant.
        /// </summary>
        /// <param name="ciphertext">The ciphertext to decrypt (includes IV as first 8 bytes).</param>
        /// <param name="key">The decryption key (must be exactly 16 bytes).</param>
        /// <returns>The decrypted plaintext.</returns>
        public static byte[] Decrypt(byte[] ciphertext, byte[] key)
        {
            if (ciphertext == null || ciphertext.Length < 9 || ciphertext.Length % 8 != 0)
                throw new ArgumentException(
                    "Ciphertext must include IV (at least 9 bytes) and be multiple of 8 bytes in length",
                    nameof(ciphertext));

            if (key is not { Length: 16 })
                throw new ArgumentException("Key must be exactly 16 bytes for Speck64/128", nameof(key));

            // Extract IV from first block
            byte[] iv = new byte[8];
            Array.Copy(ciphertext, 0, iv, 0, 8);

            // Generate subkeys
            uint[] subkeys = GenerateSubkeys64_128(key);

            // Prepare output (plaintext without IV)
            int plaintextLength = ciphertext.Length - 8;
            byte[] plaintext = new byte[plaintextLength];

            // Previous block for CBC chaining (initially the IV)
            byte[] previousBlock = iv;

            // Process each block
            for (int i = 0; i < plaintextLength; i += 8)
            {
                // Current ciphertext block
                byte[] currentBlock = new byte[8];
                Array.Copy(ciphertext, 8 + i, currentBlock, 0, 8);

                // Extract words
                uint x = BitwiseUtils.U8To32Little(currentBlock, 0);
                uint y = BitwiseUtils.U8To32Little(currentBlock, 4);

                // Decrypt block
                (x, y) = DecryptBlock(x, y, subkeys);

                // Store result
                byte[] decryptedBlock = new byte[8];
                BitwiseUtils.ToBytes(decryptedBlock, x, 0);
                BitwiseUtils.ToBytes(decryptedBlock, y, 4);

                // XOR with previous ciphertext block or IV
                for (int j = 0; j < 8; j++)
                {
                    plaintext[i + j] = (byte)(decryptedBlock[j] ^ previousBlock[j]);
                }

                // Update previous block for next iteration
                previousBlock = currentBlock;
            }

            return plaintext;
        }

        /// <summary>
        /// Decrypts data using CBC mode with the Speck64/128 variant.
        /// </summary>
        /// <param name="ciphertext">The ciphertext to decrypt (includes IV as first 8 bytes).</param>
        /// <param name="key">The decryption key (must be exactly 16 bytes).</param>
        /// <param name="plaintext">The decrypted plaintext.</param>
        /// <exception cref="ArgumentException"></exception>
        public static void Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> key, Span<byte> plaintext)
        {
            if (ciphertext.Length < 9 || ciphertext.Length % 8 != 0)
                throw new ArgumentException(
                    "Ciphertext must include IV (at least 9 bytes) and be multiple of 8 bytes in length",
                    nameof(ciphertext));

            if (key.Length != 16)
                throw new ArgumentException("Key must be exactly 16 bytes for Speck64/128", nameof(key));

            // Extract IV from first block
            ReadOnlySpan<byte> iv = ciphertext[..8];

            // Generate subkeys
            uint[] subkeys = GenerateSubkeys64_128(key.ToArray());

            // Prepare output (plaintext without IV)
            int plaintextLength = ciphertext.Length - 8;

            // Previous block for CBC chaining (initially the IV)
            ReadOnlySpan<byte> previousBlock = iv;

            // Process each block
            for (int i = 0; i < plaintextLength; i += 8)
            {
                ReadOnlySpan<byte> currentBlock = ciphertext.Slice(8 + i, 8);

                // Extract words
                uint x = BitwiseUtils.U8To32Little(currentBlock, 0);
                uint y = BitwiseUtils.U8To32Little(currentBlock, 4);

                // Decrypt block
                (x, y) = DecryptBlock(x, y, subkeys);

                // XOR with previous ciphertext block or IV
                Span<byte> decryptedBlock = new byte[8];
                BitwiseUtils.ToBytes(decryptedBlock, x, 0);
                BitwiseUtils.ToBytes(decryptedBlock, y, 4);

                // XOR with previous ciphertext block or IV
                for (int j = 0; j < 8; j++)
                {
                    plaintext[i + j] = (byte)(decryptedBlock[j] ^ previousBlock[j]);
                }

                // Update previous block for next iteration
                previousBlock = currentBlock;
            }
        }
    }

    #endregion

    #region Private API

    /// <summary>
    /// Encrypts a single block using Speck algorithm
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (uint x, uint y) EncryptBlock(uint x, uint y, uint[] subkeys)
    {
        for (int i = 0; i < subkeys.Length; i++)
        {
            x = Round(x, y, subkeys[i]);
            // Swap x and y for next round
            (x, y) = (y, x);
        }

        return (x, y);
    }

    /// <summary>
    /// Decrypts a single block using Speck algorithm
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (uint x, uint y) DecryptBlock(uint x, uint y, uint[] subkeys)
    {
        for (int i = subkeys.Length - 1; i >= 0; i--)
        {
            // Swap x and y first (reverse of encryption)
            (x, y) = (y, x);
            x = InverseRound(x, y, subkeys[i]);
        }

        return (x, y);
    }

    /// <summary>
    /// Generates the round subkeys for Speck64/128 from the 128-bit master key.
    /// </summary>
    /// <param name="key">The 16-byte (128-bit) master key.</param>
    /// <returns>Array of round subkeys.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint[] GenerateSubkeys64_128(byte[] key)
    {
        // For Speck64/128, we need 27 rounds
        const int rounds = 27;
        uint[] subkeys = new uint[rounds];

        // Initialize key parts
        uint k0 = BitwiseUtils.U8To32Little(key, 0);  // l[0]
        uint k1 = BitwiseUtils.U8To32Little(key, 4);  // l[1] 
        uint k2 = BitwiseUtils.U8To32Little(key, 8);  // l[2]
        uint k3 = BitwiseUtils.U8To32Little(key, 12); // k

        // First subkey is k3
        subkeys[0] = k3;

        // Generate remaining subkeys
        for (int i = 0; i < rounds - 1; i++)
        {
            uint tmp = k0;
            k0 = Round(k0, k3, (uint)i);
            k3 = k0;

            // Rotate through key parts
            k0 = k1;
            k1 = k2;
            k2 = tmp;

            // Add to subkeys array
            subkeys[i + 1] = k3;
        }

        return subkeys;
    }

    /// <summary>
    /// Performs a single round of the Speck cipher on two data words.
    /// </summary>
    /// <param name="x">First data word.</param>
    /// <param name="y">Second data word.</param>
    /// <param name="k">Round subkey.</param>
    /// <returns>The transformed first data word.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0059:Unnecessary assignment of a value", Justification = "<Pending>")]
    private static uint Round(uint x, uint y, uint k)
    {
        x = BitwiseUtils.RotateRight(x, (int)ALPHA); // x = ROR(x, alpha)
        x = BitwiseUtils.Add(x, y);                  // x = (x + y) mod 2^32
        x = BitwiseUtils.XOr(x, k);                  // x = x ⊕ k
        y = BitwiseUtils.RotateLeft(y, (int)BETA);   // y = ROL(y, beta)
        y = BitwiseUtils.XOr(y, x);                  // y = y ⊕ x

        return x;
    }

    /// <summary>
    /// Performs a single inverse round of the Speck cipher on two data words for decryption.
    /// </summary>
    /// <param name="x">First data word.</param>
    /// <param name="y">Second data word.</param>
    /// <param name="k">Round subkey.</param>
    /// <returns>The transformed first data word.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint InverseRound(uint x, uint y, uint k)
    {
        y = BitwiseUtils.XOr(y, x);                 // Inverse of: y = y ⊕ x
        y = BitwiseUtils.RotateRight(y, (int)BETA); // Inverse of: y = ROL(y, beta)
        x = BitwiseUtils.XOr(x, k);                 // Inverse of: x = x ⊕ k
        x = BitwiseUtils.Subtract(x, y);            // Inverse of: x = (x + y) mod 2^32
        x = BitwiseUtils.RotateLeft(x, (int)ALPHA); // Inverse of: x = ROR(x, alpha)

        return x;
    }

    #endregion
}
