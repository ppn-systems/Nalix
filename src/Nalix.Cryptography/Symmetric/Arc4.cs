namespace Nalix.Cryptography.Symmetric;

/// <summary>
/// Implements the ARC4 (Alleged RC4) symmetric stream cipher.
/// WARNING: ARC4 is considered cryptographically weak and should not be used for new applications.
/// Consider using more secure alternatives like ChaCha20 or AES-GCM.
/// </summary>
public sealed class Arc4 : System.IDisposable
{
    #region Constants

    private const System.Int32 PermutationSize = 256;
    private const System.Int32 WeakKeyMitigationBytes = 3072;

    #endregion Constants

    #region Fields

    // Store state as individual fields for better performance
    private System.Byte _i;

    private System.Byte _j;
    private readonly System.Byte[] _s = new System.Byte[256];

    private System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Arc4"/> class with the given key.
    /// </summary>
    /// <param name="key">The encryption/decryption key (should be between 5 and 256 bytes).</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if the key is <c>null</c>.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    /// Thrown if the key is shorter than 5 bytes or longer than 256 bytes.
    /// </exception>
    public Arc4(System.ReadOnlySpan<System.Byte> key)
    {
        if (key.IsEmpty)
            throw new System.ArgumentNullException(nameof(key));

        if (key.Length < 5 || key.Length > 256)
            throw new System.ArgumentException("Key length must be between 5 and 256 bytes.", nameof(key));

        this.Initialize(key);
    }

    #endregion Constructors

    #region Public API

    /// <summary>
    /// Encrypts or decrypts the given data in-place using the ARC4 stream cipher.
    /// </summary>
    /// <param name="buffer">The data buffer to be encrypted or decrypted.</param>
    /// <exception cref="System.ObjectDisposedException">Thrown if this instance has been disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Process(System.Span<System.Byte> buffer)
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);

        // Process 4 bytes at a time when possible
        System.Int32 blockCount = buffer.Length / 4;
        System.Int32 remainder = buffer.Length % 4;

        if (blockCount > 0)
        {
            this.ProcessBlocks(System.Runtime.InteropServices.MemoryMarshal
                .Cast<System.Byte, System.UInt32>(buffer[..(blockCount * 4)]));
        }

        // Process any remaining bytes
        if (remainder > 0)
        {
            this.ProcessBytes(buffer[(blockCount * 4)..]);
        }
    }

    /// <summary>
    /// Resets the internal state of the cipher.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        System.ObjectDisposedException.ThrowIf(_disposed, this);
        _i = 0;
        _j = 0;
        System.Array.Clear(_s, 0, _s.Length);
    }

    /// <summary>
    /// Disposes the resources used by this instance.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (!_disposed)
        {
            // Clear the state array to remove sensitive data
            System.Array.Clear(_s, 0, _s.Length);
            _disposed = true;
        }
    }

    #endregion Public API

    #region Private API

    /// <summary>
    /// Initializes the cipher with the provided key.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void Initialize(System.ReadOnlySpan<System.Byte> key)
    {
        // Initialize the permutation array (S)
        for (System.Int32 k = 0; k < PermutationSize; k++)
        {
            _s[k] = (System.Byte)k;
        }

        // Perform Key Scheduling Algorithm (KSA)
        System.Byte j = 0;
        for (System.Int32 k = 0; k < PermutationSize; k++)
        {
            j += (System.Byte)(_s[k] + key[k % key.Length]);
            (_s[k], _s[j]) = (_s[j], _s[k]); // Swap values in the permutation array
        }

        // Drop the first 3072 bytes to mitigate weak key initial state vulnerabilities
        _i = 0;
        _j = 0;

        System.Span<System.Byte> dummy = stackalloc System.Byte[WeakKeyMitigationBytes];
        this.Process(dummy); // Process the dummy bytes to discard the weak initial state
    }

    /// <summary>
    /// Processes blocks of 4 bytes at a time for better performance.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ProcessBlocks(System.Span<System.UInt32> blocks)
    {
        for (System.Int32 i = 0; i < blocks.Length; i++)
        {
            System.UInt32 keystream = 0;

            // Unroll the loop to process 4 bytes directly
            _i++;
            _j += _s[_i];
            (_s[_i], _s[_j]) = (_s[_j], _s[_i]);
            keystream |= (System.UInt32)_s[(System.Byte)(_s[_i] + _s[_j])] << 24;

            _i++;
            _j += _s[_i];
            (_s[_i], _s[_j]) = (_s[_j], _s[_i]);
            keystream |= (System.UInt32)_s[(System.Byte)(_s[_i] + _s[_j])] << 16;

            _i++;
            _j += _s[_i];
            (_s[_i], _s[_j]) = (_s[_j], _s[_i]);
            keystream |= (System.UInt32)_s[(System.Byte)(_s[_i] + _s[_j])] << 8;

            _i++;
            _j += _s[_i];
            (_s[_i], _s[_j]) = (_s[_j], _s[_i]);
            keystream |= _s[(System.Byte)(_s[_i] + _s[_j])];

            // XOR the block with the keystream
            blocks[i] ^= keystream;
        }
    }

    /// <summary>
    /// Processes individual bytes (used for the remainder).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ProcessBytes(System.Span<System.Byte> buffer)
    {
        for (System.Int32 k = 0; k < buffer.Length; k++)
        {
            _i++;
            _j += _s[_i];
            (_s[_i], _s[_j]) = (_s[_j], _s[_i]);

            // Compute keystream byte and XOR it with the buffer
            System.Byte keystream = _s[(System.Byte)(_s[_i] + _s[_j])];
            buffer[k] ^= keystream;
        }
    }

    #endregion Private API
}