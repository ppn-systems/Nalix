// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Exceptions;

namespace Nalix.Shared.Security.Internal;

[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static class ThrowHelper
{
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowNotSupportedException(System.String message) => throw new CryptographyException(message);

    /// <summary>
    /// Throws an <see cref="CryptographyException"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowArgumentNullException(System.String paramName) => throw new CryptographyException(paramName);

    /// <summary>
    /// Throws an <see cref="System.ArgumentException"/> for an invalid cryptographic key length.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowInvalidKeyLengthException(System.String paramName = "key")
        => throw new CryptographyException($"The key length is invalid. {paramName}");

    /// <summary>
    /// Throws an <see cref="CryptographyException"/> for an invalid nonce length.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowInvalidNonceLengthException(System.String paramName = "nonce")
        => throw new CryptographyException($"The nonce length is invalid. {paramName}");

    /// <summary>
    /// Throws an <see cref="CryptographyException"/> for an invalid authentication tag length.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowInvalidTagLengthException(System.String paramName = "tag")
        => throw new CryptographyException($"The authentication tag length is invalid. {paramName}");

    /// <summary>
    /// Throws an <see cref="CryptographyException"/> when output length does not match input length.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowOutputLengthMismatchException()
        => throw new CryptographyException("The output length does not match the input length.");

    /// <summary>
    /// Throws an <see cref="CryptographyException"/> when the ciphertext buffer is too small.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowCiphertextTooShortException(System.String paramName = "ciphertext")
        => throw new CryptographyException($"The ciphertext buffer is too small. {paramName}");
}