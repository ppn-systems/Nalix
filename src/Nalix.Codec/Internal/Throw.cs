// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Nalix.Abstractions.Exceptions;

namespace Nalix.Codec.Internal;

/// <summary>
/// Provides cached, zero-allocation exception instances for Abstractions framework errors.
/// These instances avoid the overhead of stack trace generation by overriding the StackTrace property.
/// </summary>
internal static class Throw
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

    public static readonly CipherException CipherAeadAuthenticationFailed =
        new CachedCipherException("AEAD authentication failed.");

    public static readonly CipherException CipherUnsupportedAlgorithm =
        new CachedCipherException("Unsupported cipher algorithm.");

    #endregion Security Errors

    #region Resource Errors

    public static readonly InternalErrorException SerializationFixedBufferExpansion =
        new CachedInternalErrorException("Cannot expand a fixed-size serialization buffer.");

    public static readonly ArgumentOutOfRangeException WriterAdvanceOutOfBound =
        new CachedArgumentOutOfRangeException("count", "Advance out of buffer bounds.");

    #endregion Resource Errors

    #region Transform Errors

    public static readonly LZ4Exception TransformSourceTooSmallForLZ4Header =
        new CachedLZ4Exception("The source buffer is too small to contain an LZ4 block header.");

    public static readonly LZ4Exception TransformInvalidDecompressedLength =
        new CachedLZ4Exception("LZ4 header declares an invalid OriginalLength. Must be in range [1, MaxDecompressedSize].");

    public static readonly CipherException TransformEncryptionKeyEmpty =
        new CachedCipherException("Encryption key cannot be null or empty.");

    public static readonly InternalErrorException TransformSourceTooSmall =
        new CachedInternalErrorException("Source too small: buffer must be larger than the header offset.");

    public static readonly InternalErrorException TransformDestinationTooSmall =
        new CachedInternalErrorException("Destination too small: capacity must be at least the header offset.");

    public static readonly InternalErrorException TransformBufferTooSmallForPacket =
        new CachedInternalErrorException("The source and destination buffers must contain a packet header and be large enough for the payload.");

    public static readonly CipherException TransformCiphertextFrameTooShort =
        new CachedCipherException("Ciphertext frame is too short to contain a valid envelope header.");

    public static readonly CipherException TransformEncryptedButNoCipher =
        new CachedCipherException("Encrypted frame received but no cipher suite has been negotiated.");

    public static readonly CipherException TransformEncryptedButNoKey =
        new CachedCipherException("Encrypted frame received before session key establishment.");

    public static readonly CipherException TransformEncryptRequestedButNoCipher =
        new CachedCipherException("Encryption requested but no cipher suite has been negotiated.");

    #endregion Transform Errors

    #region Throw Helpers — LZ4

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidHeader() => throw LZ4InvalidHeader;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void CorruptPayload() => throw LZ4CorruptPayload;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void OutputBufferTooSmall() => throw LZ4OutputBufferTooSmall;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void EncoderOutputBufferTooSmall() => throw LZ4EncoderOutputBufferTooSmall;

    #endregion Throw Helpers — LZ4

    #region Throw Helpers — Serialization

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void EmptyBuffer() => throw SerializationEmptyBuffer;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void BufferTooSmall() => throw SerializationBufferTooSmall;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void EndOfStream() => throw SerializationEndOfStream;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Overflow() => throw SerializationOverflow;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void StringTooLong() => throw SerializationStringTooLong;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void LengthOutOfRange() => throw SerializationLengthOutOfRange;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void DataMismatch() => throw SerializationDataMismatch;

    #endregion Throw Helpers — Serialization

    #region Throw Helpers — Security

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void OutputLengthMismatch() => throw CipherOutputLengthMismatch;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidKeyLength() => throw CipherInvalidKeyLength;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidNonceLength() => throw CipherInvalidNonceLength;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidTagLength() => throw CipherInvalidTagLength;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void CiphertextBufferTooShort() => throw CiphertextTooShort;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AeadAuthenticationFailed() => throw CipherAeadAuthenticationFailed;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void UnsupportedAlgorithm() => throw CipherUnsupportedAlgorithm;

    #endregion Throw Helpers — Security

    #region Throw Helpers — Resource

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void FixedBufferExpansion() => throw SerializationFixedBufferExpansion;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AdvanceOutOfBound() => throw WriterAdvanceOutOfBound;

    #endregion Throw Helpers — Resource

    #region Throw Helpers — Transform

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void SourceTooSmallForLZ4Header() => throw TransformSourceTooSmallForLZ4Header;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidDecompressedLength() => throw TransformInvalidDecompressedLength;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void EncryptionKeyEmpty() => throw TransformEncryptionKeyEmpty;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void SourceTooSmall() => throw TransformSourceTooSmall;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void DestinationTooSmall() => throw TransformDestinationTooSmall;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void BufferTooSmallForPacket() => throw TransformBufferTooSmallForPacket;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void CiphertextFrameTooShort() => throw TransformCiphertextFrameTooShort;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void EncryptedButNoCipher() => throw TransformEncryptedButNoCipher;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void EncryptedButNoKey() => throw TransformEncryptedButNoKey;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void EncryptRequestedButNoCipher() => throw TransformEncryptRequestedButNoCipher;

    #endregion Throw Helpers — Transform

    #region Private Cached Exception Types

    private sealed class CachedSerializationException(string message) : SerializationFailureException(message)
    {
        public override string? StackTrace => "   at Nalix.Codec.Serialization (Cached Exception)";
    }

    private sealed class CachedCipherException(string message) : CipherException(message)
    {
        public override string? StackTrace => "   at Nalix.Codec.Security (Cached Exception)";
    }

    private sealed class CachedInternalErrorException(string message) : InternalErrorException(message)
    {
        public override string? StackTrace => "   at Nalix.Codec (Cached Exception)";
    }

    private sealed class CachedLZ4Exception(string message) : LZ4Exception(message)
    {
        public override string? StackTrace => "   at Nalix.Codec.LZ4 (Cached Exception)";
    }

    private sealed class CachedArgumentOutOfRangeException(string paramName, string message) : ArgumentOutOfRangeException(paramName, message)
    {
        public override string? StackTrace => "   at Nalix.Codec.Memory (Cached Exception)";
    }

    #endregion Private Cached Exception Types
}
