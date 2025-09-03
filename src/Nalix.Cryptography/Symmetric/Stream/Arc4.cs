// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Cryptography.Symmetric.Stream;

/// <summary>
/// Implements the ARC4 (Alleged RC4) symmetric stream cipher.
/// <para><b>WARNING</b>: ARC4 is considered cryptographically weak. Prefer ChaCha20 or AES-GCM for new code.</para>
/// </summary>
[System.Obsolete("ARC4 is considered cryptographically weak. Prefer ChaCha20 or AES-GCM for new code.", false)]
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed class Arc4 : System.IDisposable
{
    #region Constants

    private const System.Int32 PermutationSize = 256;

    // Drop initial bytes to mitigate weak initial state (so-called RC4-drop[n]).
    // 3072 is a conservative choice used in some legacy systems.
    private const System.Int32 WeakKeyMitigationBytes = 3072;

    #endregion

    #region Fields

    // Indices (byte wrap-around is desired behavior in RC4)
    private System.Byte _i;
    private System.Byte _j;

    // State permutation S
    private readonly System.Byte[] _s = new System.Byte[PermutationSize];

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

        Initialize(key);
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
            Process4ByteBlocks(buffer[..fast]);
        }

        if (fast != len)
        {
            ProcessRemainder(buffer[fast..]);
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
            destination[i + 0] = (System.Byte)(source[i + 0] ^ NextKeystreamByte());
            destination[i + 1] = (System.Byte)(source[i + 1] ^ NextKeystreamByte());
            destination[i + 2] = (System.Byte)(source[i + 2] ^ NextKeystreamByte());
            destination[i + 3] = (System.Byte)(source[i + 3] ^ NextKeystreamByte());
        }

        for (; i < len; i++)
        {
            destination[i] = (System.Byte)(source[i] ^ NextKeystreamByte());
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

        Initialize(key);
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

    #region Private API

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void Initialize(System.ReadOnlySpan<System.Byte> key)
    {
        // 1) Init S
        for (System.Int32 k = 0; k < PermutationSize; k++)
        {
            _s[k] = (System.Byte)k;
        }

        // 2) KSA
        System.Byte j = 0;
        System.Int32 keyLen = key.Length;
        System.Int32 keyIndex = 0;

        for (System.Int32 k = 0; k < PermutationSize; k++)
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
        if (WeakKeyMitigationBytes > 0)
        {
            for (System.Int32 n = 0; n < WeakKeyMitigationBytes; n++)
            {
                _ = NextKeystreamByte();
            }
        }

        // 4) Mark ready
        _initialized = true;
    }

    // Generate next keystream byte (PRGA step)
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Byte NextKeystreamByte()
    {
        _i++;
        _j += _s[_i];
        (_s[_i], _s[_j]) = (_s[_j], _s[_i]);
        return _s[(System.Byte)(_s[_i] + _s[_j])];
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void Process4ByteBlocks(System.Span<System.Byte> buffer)
    {
        // Unrolled 4 bytes per iteration (byte-wise to avoid endianness pitfalls)
        for (System.Int32 k = 0; k < buffer.Length; k += 4)
        {
            buffer[k + 0] ^= NextKeystreamByte();
            buffer[k + 1] ^= NextKeystreamByte();
            buffer[k + 2] ^= NextKeystreamByte();
            buffer[k + 3] ^= NextKeystreamByte();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ProcessRemainder(System.Span<System.Byte> buffer)
    {
        for (System.Int32 k = 0; k < buffer.Length; k++)
        {
            buffer[k] ^= NextKeystreamByte();
        }
    }

    #endregion
}
