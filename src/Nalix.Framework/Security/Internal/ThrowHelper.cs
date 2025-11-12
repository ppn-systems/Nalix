// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.


// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Exceptions;

namespace Nalix.Framework.Security.Internal;

[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static class ThrowHelper
{
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowNotSupportedException(string message) => throw new CryptographyException(message);

    /// <summary>
    /// Throws an <see cref="CryptographyException"/>.
    /// </summary>
    /// <param name="paramName"></param>
    /// <exception cref="CryptographyException"></exception>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowArgumentNullException(string paramName) => throw new CryptographyException(paramName);

    /// <summary>
    /// Throws an <see cref="System.ArgumentException"/> for an invalid cryptographic key length.
    /// </summary>
    /// <param name="paramName"></param>
    /// <exception cref="CryptographyException"></exception>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowInvalidKeyLengthException(string paramName = "key")
        => throw new CryptographyException($"The key length is invalid. {paramName}");

    /// <summary>
    /// Throws an <see cref="CryptographyException"/> for an invalid nonce length.
    /// </summary>
    /// <param name="paramName"></param>
    /// <exception cref="CryptographyException"></exception>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowInvalidNonceLengthException(string paramName = "nonce")
        => throw new CryptographyException($"The nonce length is invalid. {paramName}");

    /// <summary>
    /// Throws an <see cref="CryptographyException"/> for an invalid authentication tag length.
    /// </summary>
    /// <param name="paramName"></param>
    /// <exception cref="CryptographyException"></exception>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowInvalidTagLengthException(string paramName = "tag")
        => throw new CryptographyException($"The authentication tag length is invalid. {paramName}");

    /// <summary>
    /// Throws an <see cref="CryptographyException"/> when output length does not match input length.
    /// </summary>
    /// <exception cref="CryptographyException"></exception>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowOutputLengthMismatchException()
        => throw new CryptographyException("The output length does not match the input length.");

    /// <summary>
    /// Throws an <see cref="CryptographyException"/> when the ciphertext buffer is too small.
    /// </summary>
    /// <param name="paramName"></param>
    /// <exception cref="CryptographyException"></exception>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void ThrowCiphertextTooShortException(string paramName = "ciphertext")
        => throw new CryptographyException($"The ciphertext buffer is too small. {paramName}");
}
