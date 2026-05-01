// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.


// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Exceptions;
using Nalix.Codec.Internal;

namespace Nalix.Codec.Security.Internal;

[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static class ThrowHelper
{
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowNotSupportedException(string message) => throw new CipherException(message);

    /// <summary>
    /// Throws an <see cref="CipherException"/>.
    /// </summary>
    /// <param name="paramName"></param>
    /// <exception cref="CipherException">Always thrown to signal a missing required cryptographic argument.</exception>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowArgumentNullException(string paramName) => throw new CipherException(paramName);

    /// <summary>
    /// Throws an <see cref="System.ArgumentException"/> for an invalid cryptographic key length.
    /// </summary>
    /// <param name="paramName"></param>
    /// <exception cref="CipherException">Always thrown to signal an invalid key length.</exception>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowInvalidKeyLengthException(string paramName = "key")
    {
        if (paramName == "key")
        {
            throw CodecErrors.CipherInvalidKeyLength;
        }

        throw new CipherException($"The key length is invalid. {paramName}");
    }

    /// <summary>
    /// Throws an <see cref="CipherException"/> for an invalid nonce length.
    /// </summary>
    /// <param name="paramName"></param>
    /// <exception cref="CipherException">Always thrown to signal an invalid nonce length.</exception>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowInvalidNonceLengthException(string paramName = "nonce")
    {
        if (paramName == "nonce")
        {
            throw CodecErrors.CipherInvalidNonceLength;
        }

        throw new CipherException($"The nonce length is invalid. {paramName}");
    }

    /// <summary>
    /// Throws an <see cref="CipherException"/> for an invalid authentication tag length.
    /// </summary>
    /// <param name="paramName"></param>
    /// <exception cref="CipherException">Always thrown to signal an invalid authentication tag length.</exception>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowInvalidTagLengthException(string paramName = "tag")
    {
        if (paramName == "tag")
        {
            throw CodecErrors.CipherInvalidTagLength;
        }

        throw new CipherException($"The authentication tag length is invalid. {paramName}");
    }

    /// <summary>
    /// Throws an <see cref="CipherException"/> when output length does not match input length.
    /// </summary>
    /// <exception cref="CipherException">Always thrown when cipher output length validation fails.</exception>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowOutputLengthMismatchException()
        => throw CodecErrors.CipherOutputLengthMismatch;

    /// <summary>
    /// Throws an <see cref="CipherException"/> when the ciphertext buffer is too small.
    /// </summary>
    /// <param name="paramName"></param>
    /// <exception cref="CipherException">Always thrown when the ciphertext buffer is shorter than required.</exception>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowCiphertextTooShortException(string paramName = "ciphertext")
    {
        if (paramName == "ciphertext")
        {
            throw CodecErrors.CiphertextTooShort;
        }

        throw new CipherException($"The ciphertext buffer is too small. {paramName}");
    }
}
