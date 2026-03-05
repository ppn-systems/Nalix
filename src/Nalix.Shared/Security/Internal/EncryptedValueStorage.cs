// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Shared.Security.Internal;

/// <summary>
/// Holds the encrypted representation of a value-type member (int, bool, struct, enum, etc.)
/// that cannot be stored back in-place as a string.
/// Instances are kept in a <see cref="System.Runtime.CompilerServices.ConditionalWeakTable{TKey,TValue}"/>
/// keyed by the owning object, so they are collected alongside it.
/// </summary>
internal sealed class EncryptedValueStorage
{
    /// <summary>The original CLR type needed to deserialize back to the correct value.</summary>
    public required System.Type OriginalType { get; init; }

    /// <summary>Base64-encoded ciphertext produced by <see cref="EnvelopeValueCodec"/>.</summary>
    public required System.String EncryptedBase64 { get; init; }
}
