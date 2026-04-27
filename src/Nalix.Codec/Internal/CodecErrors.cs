// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Exceptions;

namespace Nalix.Codec.Internal;

/// <summary>
/// Provides cached, zero-allocation exception instances for common framework errors.
/// These instances avoid the overhead of stack trace generation by overriding the StackTrace property.
/// </summary>
internal static class CodecErrors
{
    #region LZ4 Errors

    public static readonly LZ4Exception LZ4InvalidHeader =
        new CachedLZ4Exception("The LZ4 block header is invalid or corrupt.");

    public static readonly LZ4Exception LZ4CorruptPayload =
        new CachedLZ4Exception("The LZ4 payload is malformed or corrupt.");

    public static readonly LZ4Exception LZ4OutputBufferTooSmall =
        new CachedLZ4Exception("The output buffer is too small for the decompressed LZ4 payload.");

    public static readonly LZ4Exception LZ4EncoderOutputBufferTooSmall =
        new CachedLZ4Exception("LZ4 compression failed: the destination buffer is too small.");

    #endregion LZ4 Errors

    #region Serialization Errors

    public static readonly SerializationFailureException SerializationEmptyBuffer =
        new CachedSerializationException("Cannot deserialize from an empty buffer.");

    public static readonly SerializationFailureException SerializationBufferTooSmall =
        new CachedSerializationException("The provided buffer is too small for the operation.");

    public static readonly SerializationFailureException SerializationEndOfStream =
        new CachedSerializationException("The end of the stream was reached prematurely.");

    public static readonly SerializationFailureException SerializationOverflow =
        new CachedSerializationException("Serialization data size overflow.");

    public static readonly SerializationFailureException SerializationStringTooLong =
        new CachedSerializationException("The string exceeds the allowed limit.");

    public static readonly SerializationFailureException SerializationLengthOutOfRange =
        new CachedSerializationException("The provided length is out of range.");

    public static readonly SerializationFailureException SerializationDataMismatch =
        new CachedSerializationException("Serialization data mismatch.");

    #endregion Serialization Errors

    #region Security Errors

    public static readonly CipherException CipherOutputLengthMismatch =
        new CachedCipherException("The output length does not match the input length.");

    public static readonly CipherException CipherInvalidKeyLength =
        new CachedCipherException("The key length is invalid.");

    public static readonly CipherException CipherInvalidNonceLength =
        new CachedCipherException("The nonce length is invalid.");

    public static readonly CipherException CipherInvalidTagLength =
        new CachedCipherException("The authentication tag length is invalid.");

    public static readonly CipherException CiphertextTooShort =
        new CachedCipherException("The ciphertext buffer is too small.");

    #endregion Security Errors

    #region Resource Errors

    public static readonly InvalidOperationException SerializationFixedBufferExpansion =
        new CachedInvalidOperationException("Cannot expand a fixed-size serialization buffer.");

    #endregion Resource Errors

    #region Private Cached Exception Types

    private sealed class CachedSerializationException(string message) : SerializationFailureException(message)
    {
        public override string? StackTrace => "   at Nalix.Codec.Serialization (Cached Exception)";
    }

    private sealed class CachedCipherException(string message) : CipherException(message)
    {
        public override string? StackTrace => "   at Nalix.Codec.Security (Cached Exception)";
    }

    private sealed class CachedInvalidOperationException(string message) : InvalidOperationException(message)
    {
        public override string? StackTrace => "   at Nalix.Codec (Cached Exception)";
    }

    private sealed class CachedLZ4Exception(string message) : LZ4Exception(message)
    {
        public override string? StackTrace => "   at Nalix.Codec.LZ4 (Cached Exception)";
    }

    #endregion Private Cached Exception Types
}
