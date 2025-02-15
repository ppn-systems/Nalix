using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Notio.Cryptography.Ciphers.Mac;

/// <summary>
/// Represents the Poly1305 message authentication code (MAC) algorithm, used for ensuring the integrity and authenticity of messages.
/// </summary>
/// <remarks>
/// Poly1305 is a fast cryptographic MAC algorithm designed by Daniel J. Bernstein. It is used in various cryptographic protocols,
/// including the ChaCha20-Poly1305 cipher suite in TLS and other secure communication protocols.
/// </remarks>
public sealed class Poly1305
{
    // r: the first half of the key (clamped) and s: the second half.
    private readonly BigInteger r;

    private readonly BigInteger s;

    /// <summary>
    /// Initializes a new instance of the Poly1305 class using a 32-byte key.
    /// </summary>
    /// <param name="key">A 32-byte key. The first 16 bytes are used for r (after clamping),
    /// and the last 16 bytes are used as s.</param>
    public Poly1305(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes.", nameof(key));

        // Extract and clamp r (the first 16 bytes).
        byte[] rBytes = new byte[16];
        Array.Copy(key, 0, rBytes, 0, 16);
        // Clamp r: clear the upper bits of rBytes[3], rBytes[7], rBytes[11], and rBytes[15].
        rBytes[3] &= 0x0f;
        rBytes[7] &= 0x0f;
        rBytes[11] &= 0x0f;
        rBytes[15] &= 0x0f;
        // Extend with an extra byte to ensure non-negative BigInteger.
        byte[] rExtended = new byte[rBytes.Length + 1];
        Array.Copy(rBytes, rExtended, rBytes.Length);
        r = new BigInteger(rExtended);

        // Extract s (the last 16 bytes).
        byte[] sBytes = new byte[16];
        Array.Copy(key, 16, sBytes, 0, 16);
        byte[] sExtended = new byte[sBytes.Length + 1];
        Array.Copy(sBytes, sExtended, sBytes.Length);
        s = new BigInteger(sExtended);
    }

    /// <summary>
    /// Computes the Poly1305 MAC (Message Authentication Code) for the given message.
    /// </summary>
    /// <param name="message">The message to authenticate (as a byte array).</param>
    /// <returns>A 16-byte authentication tag.</returns>
    public byte[] ComputeTag(byte[] message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // prime = 2^130 - 5 (see RFC 8439).
        BigInteger prime = (BigInteger.One << 130) - 5;
        BigInteger accumulator = BigInteger.Zero;

        // Process the message in 16-byte blocks.
        int offset = 0;
        while (offset < message.Length)
        {
            // Determine block size (the final block may be shorter than 16 bytes).
            int blockSize = Math.Min(16, message.Length - offset);
            // Create a block of blockSize bytes and append a 0x01 byte.
            byte[] block = new byte[blockSize + 1];
            Array.Copy(message, offset, block, 0, blockSize);
            block[blockSize] = 0x01; // Append the “1” byte (giá trị 1).

            // Convert the block (in little‑endian order) to a BigInteger.
            byte[] blockExtended = new byte[block.Length + 1];
            Array.Copy(block, blockExtended, block.Length);
            BigInteger n = new(blockExtended);

            // Add block value and multiply by r modulo prime.
            accumulator = (accumulator + n) % prime;
            accumulator = accumulator * r % prime;

            offset += blockSize;
        }

        // After processing all blocks, add s modulo 2^128.
        BigInteger tagInt = (accumulator + s) % (BigInteger.One << 128);

        // Convert the tag to a 16-byte array (little‑endian).
        byte[] tag = tagInt.ToByteArray();

        // Ensure tag is exactly 16 bytes.
        if (tag.Length < 16)
            Array.Resize(ref tag, 16); // Pad with zeros if necessary.
        else if (tag.Length > 16)
            tag = [.. tag.Take(16)];

        return tag;
    }

    /// <summary>
    /// A static helper method to compute the Poly1305 MAC directly from a key and message.
    /// </summary>
    /// <param name="key">A 32-byte key.</param>
    /// <param name="message">The message to authenticate.</param>
    /// <returns>A 16-byte authentication tag.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Compute(byte[] key, byte[] message)
    {
        Poly1305 poly = new(key);
        return poly.ComputeTag(message);
    }
}
