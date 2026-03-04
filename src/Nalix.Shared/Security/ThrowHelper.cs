// Copyright (c) 2026 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Security;

[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.StackTraceHidden]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static class ThrowHelper
{
    /// <summary>
    /// Throws an <see cref="System.ArgumentNullException"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowArgumentNullException(System.String paramName)
        => throw new System.ArgumentNullException(paramName);

    /// <summary>
    /// Throws an <see cref="System.ArgumentException"/> when a string is null or empty.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowArgumentNullOrEmptyException(System.String paramName)
        => throw new System.ArgumentException("The value cannot be null or empty.", paramName);

    /// <summary>
    /// Throws an <see cref="System.ArgumentException"/> for an invalid cryptographic key length.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowInvalidKeyLengthException(System.String paramName = "key")
        => throw new System.ArgumentException("The key length is invalid.", paramName);

    /// <summary>
    /// Throws an <see cref="System.ArgumentException"/> for an invalid nonce length.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowInvalidNonceLengthException(System.String paramName = "nonce")
        => throw new System.ArgumentException("The nonce length is invalid.", paramName);

    /// <summary>
    /// Throws an <see cref="System.ArgumentException"/> for an invalid authentication tag length.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowInvalidTagLengthException(System.String paramName = "tag")
        => throw new System.ArgumentException("The authentication tag length is invalid.", paramName);

    /// <summary>
    /// Throws an <see cref="System.ArgumentException"/> when output length does not match input length.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowOutputLengthMismatchException()
        => throw new System.ArgumentException("The output length does not match the input length.");

    /// <summary>
    /// Throws an <see cref="System.ArgumentException"/> when the ciphertext buffer is too small.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowCiphertextTooShortException(System.String paramName = "ciphertext")
        => throw new System.ArgumentException("The ciphertext buffer is too small.", paramName);

    /// <summary>
    /// Throws a cryptographic operation failure.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowCryptographicException(System.String message)
        => throw new Nalix.Common.Exceptions.CryptoException(message);
}