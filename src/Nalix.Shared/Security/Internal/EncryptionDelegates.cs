// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Security.Enums;

namespace Nalix.Shared.Security.Internal;

/// <summary>
/// Cached delegates for type-safe serialization/deserialization operations
/// used by <see cref="EnvelopeValueCodec"/>.
/// </summary>
internal sealed class EncryptionDelegates
{
    /// <summary>
    /// Serializes a boxed value to an encrypted Base64 string.
    /// Signature: (value, key, algorithm, aad) → Base64
    /// </summary>
    public required System.Func<System.Object, System.Byte[], CipherSuiteType, System.Byte[], System.String> SerializeFunc { get; init; }

    /// <summary>
    /// Deserializes an encrypted Base64 string back to a boxed value.
    /// Signature: (base64, key, aad) → value
    /// </summary>
    public required System.Func<System.String, System.Byte[], System.Byte[], System.Object> DeserializeFunc { get; init; }
}

/// <summary>
/// Cached delegates for nested <c>Encrypt&lt;T&gt;</c> / <c>Decrypt&lt;T&gt;</c> calls,
/// replacing <c>GetMethod + MakeGenericMethod</c> on every nested object encounter.
/// </summary>
internal sealed class NestedEncryptorDelegates
{
    /// <summary>
    /// Calls <c>EnvelopeEncryptor.Encrypt&lt;T&gt;</c> on a boxed object instance.
    /// </summary>
    public required System.Action<System.Object, System.Byte[], CipherSuiteType, System.Byte[]> EncryptAction { get; init; }

    /// <summary>
    /// Calls <c>EnvelopeEncryptor.Decrypt&lt;T&gt;</c> on a boxed object instance.
    /// </summary>
    public required System.Action<System.Object, System.Byte[], System.Byte[]> DecryptAction { get; init; }
}
