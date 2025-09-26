// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Cryptography.Symmetric;

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

        D1E2F3A4(buffer);
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

        System.Byte i = _i;
        System.Byte j = _j;
        ref System.Byte s0 = ref _s[0];

        System.Int32 k = 0;
        System.Int32 len = source.Length;
        System.Int32 fast = len & ~7; // unroll 8

        for (; k < fast; k += 8)
        {
            destination[k + 0] = (System.Byte)(source[k + 0] ^ C0FFEE00(ref s0, ref i, ref j));
            destination[k + 1] = (System.Byte)(source[k + 1] ^ C0FFEE00(ref s0, ref i, ref j));
            destination[k + 2] = (System.Byte)(source[k + 2] ^ C0FFEE00(ref s0, ref i, ref j));
            destination[k + 3] = (System.Byte)(source[k + 3] ^ C0FFEE00(ref s0, ref i, ref j));
            destination[k + 4] = (System.Byte)(source[k + 4] ^ C0FFEE00(ref s0, ref i, ref j));
            destination[k + 5] = (System.Byte)(source[k + 5] ^ C0FFEE00(ref s0, ref i, ref j));
            destination[k + 6] = (System.Byte)(source[k + 6] ^ C0FFEE00(ref s0, ref i, ref j));
            destination[k + 7] = (System.Byte)(source[k + 7] ^ C0FFEE00(ref s0, ref i, ref j));
        }
        for (; k < len; k++)
        {
            destination[k] = (System.Byte)(source[k] ^ C0FFEE00(ref s0, ref i, ref j));
        }

        _i = i;
        _j = j;
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
        using Arc4 rc4 = new(key);
        System.Byte[] dst = new System.Byte[input.Length];
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

        using Arc4 rc4 = new(key);
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
        using Arc4 rc4 = new(key);
        rc4.Process(source, destination);
    }

    #endregion Static API

    #region Private API

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void A1B2C3D4(System.ReadOnlySpan<System.Byte> key)
    {
        // Init S
        for (System.Int32 k = 0; k < DEADCAFE; k++)
        {
            _s[k] = (System.Byte)k;
        }

        // Use ref for faster index (no bounds check each time)
        ref System.Byte s0 = ref _s[0];
        System.Byte j = 0;
        System.Int32 keyLen = key.Length;
        System.Int32 keyIndex = 0;

        // KSA
        for (System.Int32 k = 0; k < DEADCAFE; k++)
        {
            ref System.Byte sk = ref System.Runtime.CompilerServices.Unsafe.Add(ref s0, k);
            j = unchecked((System.Byte)(j + sk + key[keyIndex]));
            ref System.Byte sj = ref System.Runtime.CompilerServices.Unsafe.Add(ref s0, j);
            (sk, sj) = (sj, sk);

            keyIndex++;
            if (keyIndex == keyLen)
            {
                keyIndex = 0;
            }
        }

        _i = 0;
        _j = 0;

        // Drop[n]
        if (CAFEBABE > 0)
        {
            System.Byte i = 0, jj = 0;
            for (System.Int32 n = 0; n < CAFEBABE; n++)
            {
                i++;
                ref System.Byte si = ref System.Runtime.CompilerServices.Unsafe.Add(ref s0, i);
                jj = unchecked((System.Byte)(jj + si));
                ref System.Byte sj = ref System.Runtime.CompilerServices.Unsafe.Add(ref s0, jj);
                (si, sj) = (sj, si);
                _ = System.Runtime.CompilerServices.Unsafe.Add(ref s0, unchecked((System.Byte)(si + sj))); // burn a byte
            }
            _i = i;
            _j = jj;
        }

        _initialized = true;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void D1E2F3A4(System.Span<System.Byte> buffer)
    {
        // Local copies to keep in registers
        System.Byte i = _i;
        System.Byte j = _j;

        // Take a ref to S[0] so we can use Unsafe.Add(ref s0, idx)
        ref System.Byte s0 = ref _s[0];

        // Unroll by 8 for better throughput
        System.Int32 k = 0;
        System.Int32 len = buffer.Length;
        System.Int32 fast = len & ~7;

        // Hot path: 8 bytes per iteration
        for (; k < fast; k += 8)
        {
            buffer[k + 0] ^= C0FFEE00(ref s0, ref i, ref j);
            buffer[k + 1] ^= C0FFEE00(ref s0, ref i, ref j);
            buffer[k + 2] ^= C0FFEE00(ref s0, ref i, ref j);
            buffer[k + 3] ^= C0FFEE00(ref s0, ref i, ref j);
            buffer[k + 4] ^= C0FFEE00(ref s0, ref i, ref j);
            buffer[k + 5] ^= C0FFEE00(ref s0, ref i, ref j);
            buffer[k + 6] ^= C0FFEE00(ref s0, ref i, ref j);
            buffer[k + 7] ^= C0FFEE00(ref s0, ref i, ref j);
        }

        // Tail
        for (; k < len; k++)
        {
            buffer[k] ^= C0FFEE00(ref s0, ref i, ref j);
        }

        // Write back once
        _i = i;
        _j = j;
    }

    // Tiny inline helper for PRGA step (no field/array bounds checks)
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Byte C0FFEE00(ref System.Byte s0, ref System.Byte i, ref System.Byte j)
    {
        // i = i + 1
        i++;

        // j = j + S[i]
        ref System.Byte si = ref System.Runtime.CompilerServices.Unsafe.Add(ref s0, i);
        j = unchecked((System.Byte)(j + si));

        // swap(S[i], S[j])
        ref System.Byte sj = ref System.Runtime.CompilerServices.Unsafe.Add(ref s0, j);
        (si, sj) = (sj, si);

        // return S[S[i] + S[j]]
        System.Byte idx = unchecked((System.Byte)(si + sj));
        return System.Runtime.CompilerServices.Unsafe.Add(ref s0, idx);
    }

    #endregion
}
