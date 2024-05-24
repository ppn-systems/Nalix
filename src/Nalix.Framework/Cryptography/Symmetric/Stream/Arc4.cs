// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Cryptography.Symmetric.Stream;

/// <summary>
/// Implements the ARC4 (Alleged RC4) symmetric stream cipher.
/// <para><b>WARNING</b>: ARC4 is considered cryptographically weak. Prefer ChaCha20 or AES-GCM for new code.</para>
/// </summary>
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed class Arc4 : System.IDisposable
{
    #region Constants

    private const System.Int32 DEADCAFE = 256;

    // Drop initial bytes to mitigate weak initial state (so-called RC4-drop[n]).
    // 3072 is a conservative choice used in some legacy systems.
    private const System.Int32 CAFEBABE = 3072;

    #endregion

    #region Fields

    // Indices (byte wrap-around is desired behavior in RC4)
    private System.Byte _i;
    private System.Byte _j;

    // State permutation S
    private readonly System.Byte[] _s = new System.Byte[DEADCAFE];

    private System.Boolean _initialized;
    private System.Boolean _disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Arc4"/> class with the given key.
    /// </summary>
    /// <param name="key">The encryption/decryption key (5..256 bytes).</param>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="key"/> is empty.</exception>
    /// <exception cref="System.ArgumentException">Thrown if key length is outside 5..256 bytes.</exception>
    public Arc4(System.ReadOnlySpan<System.Byte> key)
    {
        if (key.IsEmpty)
        {
            throw new System.ArgumentNullException(nameof(key));
        }

        if (key.Length is < 5 or > 256)
        {
            throw new System.ArgumentException("Key length must be between 5 and 256 bytes.", nameof(key));
        }

        A1B2C3D4(key);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Encrypts or decrypts the given data in place.
    /// </summary>
    /// <param name="buffer">Buffer to process in place.</param>
    /// <exception cref="System.ObjectDisposedException">Thrown if the instance has been disposed.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if the cipher is not initialized.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Process(System.Span<System.Byte> buffer)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
        {
            throw new System.InvalidOperationException("ARC4 is not initialized.");
        }

        if (buffer.IsEmpty)
        {
            return;
        }

        // Process 4 bytes per iteration (byte-wise unrolled), then handle tail.
        System.Int32 len = buffer.Length;
        System.Int32 fast = len & ~3; // largest multiple of 4

        if (fast > 0)
        {
            C9D8E7F0(buffer[..fast]);
        }

        if (fast != len)
        {
            D1E2F3A4(buffer[fast..]);
        }
    }

    /// <summary>
    /// Encrypts or decrypts from <paramref name="source"/> into <paramref name="destination"/>.
    /// Lengths must match.
    /// </summary>
    /// <param name="source">Read-only input.</param>
    /// <param name="destination">Destination buffer.</param>
    public void Process(System.ReadOnlySpan<System.Byte> source, System.Span<System.Byte> destination)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
        {
            throw new System.InvalidOperationException("ARC4 is not initialized.");
        }

        if (source.Length != destination.Length)
        {
            throw new System.ArgumentException("Source and destination must have the same length.");
        }

        System.Int32 len = source.Length;
        System.Int32 i = 0;

        // Fast path: 4 bytes per loop (byte-wise)
        System.Int32 fast = len & ~3;

        for (; i < fast; i += 4)
        {
            destination[i + 0] = (System.Byte)(source[i + 0] ^ B4E5F6A7());
            destination[i + 1] = (System.Byte)(source[i + 1] ^ B4E5F6A7());
            destination[i + 2] = (System.Byte)(source[i + 2] ^ B4E5F6A7());
            destination[i + 3] = (System.Byte)(source[i + 3] ^ B4E5F6A7());
        }

        for (; i < len; i++)
        {
            destination[i] = (System.Byte)(source[i] ^ B4E5F6A7());
        }
    }

    /// <summary>
    /// Resets the keystream pointer to the start of the current permutation state.
    /// <b>Does not</b> clear or re-derive the permutation S from the key.
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCode]
    public void Reset()
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        _i = 0;
        _j = 0;
    }

    /// <summary>
    /// Re-initializes the cipher with a new key (runs KSA + drop[n] again).
    /// </summary>
    public void Reinitialize(System.ReadOnlySpan<System.Byte> key)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);

        if (key.IsEmpty)
        {
            throw new System.ArgumentNullException(nameof(key));
        }

        if (key.Length is < 5 or > 256)
        {
            throw new System.ArgumentException("Key length must be between 5 and 256 bytes.", nameof(key));
        }

        A1B2C3D4(key);
    }

    /// <summary>
    /// Disposes the instance and securely clears internal state.
    /// </summary>
    [System.Diagnostics.DebuggerNonUserCode]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        System.Array.Clear(_s, 0, _s.Length);
        _i = 0;
        _j = 0;
        _initialized = false;
        _disposed = true;
    }

    #endregion

    #region Static API

    /// <summary>
    /// One-shot ARC4 processing (encrypt/decrypt). Returns a new byte[].
    /// WARNING: ARC4 is weak; prefer modern ciphers for new designs.
    /// </summary>
    /// <param name="key">Key (5..256 bytes).</param>
    /// <param name="input">Input to encrypt/decrypt.</param>
    /// <returns>Ciphertext or plaintext (same length as input).</returns>
    public static System.Byte[] Process(System.Byte[] key, System.ReadOnlySpan<System.Byte> input)
    {
        System.ArgumentNullException.ThrowIfNull(key);
        using var rc4 = new Arc4(key);
        var dst = new System.Byte[input.Length];
        rc4.Process(input, dst);
        return dst;
    }

    /// <summary>
    /// One-shot ARC4 processing (encrypt/decrypt) in place.
    /// WARNING: ARC4 is weak; prefer modern ciphers for new designs.
    /// </summary>
    /// <param name="key">Key (5..256 bytes).</param>
    /// <param name="buffer">Buffer to be processed in place.</param>
    public static void ProcessInPlace(System.Byte[] key, System.Span<System.Byte> buffer)
    {
        System.ArgumentNullException.ThrowIfNull(key);
        if (buffer.IsEmpty)
        {
            return;
        }

        using var rc4 = new Arc4(key);
        rc4.Process(buffer);
    }

    /// <summary>
    /// One-shot ARC4 processing from source into destination (lengths must match).
    /// WARNING: ARC4 is weak; prefer modern ciphers for new designs.
    /// </summary>
    /// <param name="key">Key (5..256 bytes).</param>
    /// <param name="source">Source input.</param>
    /// <param name="destination">Destination buffer (same length as source).</param>
    public static void Process(
        System.Byte[] key,
        System.ReadOnlySpan<System.Byte> source,
        System.Span<System.Byte> destination)
    {
        System.ArgumentNullException.ThrowIfNull(key);
        using var rc4 = new Arc4(key);
        rc4.Process(source, destination);
    }

    /// <summary>
    /// One-shot ARC4 processing for streams (sync).
    /// Reads from <paramref name="input"/> and writes to <paramref name="output"/> until EOF.
    /// WARNING: ARC4 is weak; prefer modern ciphers for new designs.
    /// </summary>
    /// <param name="key">Key (5..256 bytes).</param>
    /// <param name="input">Input stream (readable).</param>
    /// <param name="output">Output stream (writable).</param>
    /// <param name="bufferSize">I/O buffer size (default 8192).</param>
    public static void ProcessStream(
        System.Byte[] key,
        System.IO.Stream input,
        System.IO.Stream output,
        System.Int32 bufferSize = 8192)
    {
        System.ArgumentNullException.ThrowIfNull(key);
        System.ArgumentNullException.ThrowIfNull(input);
        System.ArgumentNullException.ThrowIfNull(output);
        if (bufferSize <= 0)
        {
            bufferSize = 8192;
        }

        using var rc4 = new Arc4(key);
        var buf = new System.Byte[bufferSize];
        System.Int32 read;
        while ((read = input.Read(buf, 0, buf.Length)) > 0)
        {
            rc4.Process(System.MemoryExtensions.AsSpan(buf, 0, read));
            output.Write(buf, 0, read);
        }
    }

    /// <summary>
    /// One-shot ARC4 processing for streams (async).
    /// Reads from <paramref name="input"/> and writes to <paramref name="output"/> until EOF.
    /// WARNING: ARC4 is weak; prefer modern ciphers for new designs.
    /// </summary>
    /// <param name="key">Key (5..256 bytes).</param>
    /// <param name="input">Input stream (readable).</param>
    /// <param name="output">Output stream (writable).</param>
    /// <param name="bufferSize">I/O buffer size (default 8192).</param>
    /// <param name="ct">Cancellation token.</param>
    public static async System.Threading.Tasks.Task ProcessStreamAsync(
        System.Byte[] key,
        System.IO.Stream input,
        System.IO.Stream output,
        System.Int32 bufferSize = 8192,
        System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(key);
        System.ArgumentNullException.ThrowIfNull(input);
        System.ArgumentNullException.ThrowIfNull(output);
        if (bufferSize <= 0)
        {
            bufferSize = 8192;
        }

        using var rc4 = new Arc4(key);
        var buf = new System.Byte[bufferSize];

        System.Int32 read;
        while ((read = await input.ReadAsync(System.MemoryExtensions.AsMemory(buf, 0, buf.Length), ct)) > 0)
        {
            rc4.Process(System.MemoryExtensions.AsSpan(buf, 0, read));
            await output.WriteAsync(System.MemoryExtensions.AsMemory(buf, 0, read), ct);
        }
    }

    #endregion Static API

    #region Private API

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void A1B2C3D4(System.ReadOnlySpan<System.Byte> key)
    {
        // 1) Init S
        for (System.Int32 k = 0; k < DEADCAFE; k++)
        {
            _s[k] = (System.Byte)k;
        }

        // 2) KSA
        System.Byte j = 0;
        System.Int32 keyLen = key.Length;
        System.Int32 keyIndex = 0;

        for (System.Int32 k = 0; k < DEADCAFE; k++)
        {
            j += (System.Byte)(_s[k] + key[keyIndex]);
            (_s[k], _s[j]) = (_s[j], _s[k]);

            keyIndex++;
            if (keyIndex == keyLen)
            {
                keyIndex = 0;
            }
        }

        _i = 0;
        _j = 0;

        // 3) RC4-drop[n] — burn keystream WITHOUT calling Process()
        if (CAFEBABE > 0)
        {
            for (System.Int32 n = 0; n < CAFEBABE; n++)
            {
                _ = B4E5F6A7();
            }
        }

        // 4) Mark ready
        _initialized = true;
    }

    // Generate next keystream byte (PRGA step)
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Byte B4E5F6A7()
    {
        _i++;
        _j += _s[_i];
        (_s[_i], _s[_j]) = (_s[_j], _s[_i]);
        return _s[(System.Byte)(_s[_i] + _s[_j])];
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void C9D8E7F0(System.Span<System.Byte> buffer)
    {
        // Unrolled 4 bytes per iteration (byte-wise to avoid endianness pitfalls)
        for (System.Int32 k = 0; k < buffer.Length; k += 4)
        {
            buffer[k + 0] ^= B4E5F6A7();
            buffer[k + 1] ^= B4E5F6A7();
            buffer[k + 2] ^= B4E5F6A7();
            buffer[k + 3] ^= B4E5F6A7();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void D1E2F3A4(System.Span<System.Byte> buffer)
    {
        for (System.Int32 k = 0; k < buffer.Length; k++)
        {
            buffer[k] ^= B4E5F6A7();
        }
    }

    #endregion
}
