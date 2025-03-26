using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Cryptography.Symmetric;

/// <summary>
/// Implements the ARC4 (Alleged RC4) symmetric stream cipher.
/// WARNING: ARC4 is considered cryptographically weak and should not be used for new applications.
/// Consider using more secure alternatives like ChaCha20 or AES-GCM.
/// </summary>
public sealed class Arc4 : IDisposable
{
    // Store state as individual fields for better performance
    private byte _i;

    private byte _j;
    private readonly byte[] _s = new byte[256];
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Arc4"/> class with the given key.
    /// </summary>
    /// <param name="key">The encryption/decryption key (should be between 5 and 256 bytes).</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the key is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if the key is shorter than 5 bytes or longer than 256 bytes.
    /// </exception>
    public Arc4(ReadOnlySpan<byte> key)
    {
        if (key.IsEmpty)
            throw new ArgumentNullException(nameof(key));

        if (key.Length < 5 || key.Length > 256)
            throw new ArgumentException("Key length must be between 5 and 256 bytes.", nameof(key));

        Initialize(key);
    }

    /// <summary>
    /// Encrypts or decrypts the given data in-place using the ARC4 stream cipher.
    /// </summary>
    /// <param name="buffer">The data buffer to be encrypted or decrypted.</param>
    /// <exception cref="ObjectDisposedException">Thrown if this instance has been disposed.</exception>
    public void Process(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Process 4 bytes at a time when possible
        int blockCount = buffer.Length / 4;
        int remainder = buffer.Length % 4;

        if (blockCount > 0)
        {
            ProcessBlocks(MemoryMarshal.Cast<byte, uint>(buffer[..(blockCount * 4)]));
        }

        // Process any remaining bytes
        if (remainder > 0)
        {
            ProcessBytes(buffer[(blockCount * 4)..]);
        }
    }

    /// <summary>
    /// Resets the internal state of the cipher.
    /// </summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _i = 0;
        _j = 0;
        Array.Clear(_s, 0, _s.Length);
    }

    /// <summary>
    /// Disposes the resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // Clear the state array to remove sensitive data
            Array.Clear(_s, 0, _s.Length);
            _disposed = true;
        }
    }

    #region Private API

    /// <summary>
    /// Initializes the cipher with the provided key.
    /// </summary>
    private void Initialize(ReadOnlySpan<byte> key)
    {
        // Initialize the permutation array
        for (int k = 0; k < 256; k++)
        {
            _s[k] = (byte)k;
        }

        // Key scheduling algorithm (KSA)
        byte j = 0;
        for (int k = 0; k < 256; k++)
        {
            j += (byte)(_s[k] + key[k % key.Length]);
            (_s[k], _s[j]) = (_s[j], _s[k]);
        }

        // Drop the first 3072 bytes to mitigate weak key initial state vulnerabilities
        _i = 0;
        _j = 0;
        Span<byte> dummy = stackalloc byte[3072];
        Process(dummy);
    }

    /// <summary>
    /// Processes blocks of 4 bytes at a time for better performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessBlocks(Span<uint> blocks)
    {
        for (int i = 0; i < blocks.Length; i++)
        {
            // Generate 4 bytes of keystream
            uint keystream = 0;

            for (int b = 0; b < 4; b++)
            {
                _i++;
                _j += _s[_i];
                (_s[_i], _s[_j]) = (_s[_j], _s[_i]);
                byte k = _s[(byte)(_s[_i] + _s[_j])];
                keystream = (keystream << 8) | k;
            }

            // XOR the block with the keystream
            blocks[i] ^= keystream;
        }
    }

    /// <summary>
    /// Processes individual bytes (used for the remainder).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessBytes(Span<byte> buffer)
    {
        for (int k = 0; k < buffer.Length; k++)
        {
            _i++;
            _j += _s[_i];
            (_s[_i], _s[_j]) = (_s[_j], _s[_i]);
            buffer[k] ^= _s[(byte)(_s[_i] + _s[_j])];
        }
    }

    #endregion
}
