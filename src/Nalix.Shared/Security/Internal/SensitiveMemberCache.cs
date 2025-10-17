// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Security.Attributes;

namespace Nalix.Shared.Security.Internal;

/// <summary>
/// Cached metadata for sensitive members of a given type,
/// including per-member sensitivity level and pre-compiled accessors.
/// </summary>
internal readonly struct SensitiveMemberCache(
    SensitiveMemberInfo[] properties,
    SensitiveMemberInfo[] fields,
    System.Int32 encryptableCount)
{
    /// <summary>
    /// Sensitive fields that carry <see cref="SensitiveDataAttribute"/>.
    /// </summary>
    public readonly SensitiveMemberInfo[] Fields = fields;

    /// <summary>
    /// Sensitive properties that carry <see cref="SensitiveDataAttribute"/>.
    /// </summary>
    public readonly SensitiveMemberInfo[] Properties = properties;

    /// <summary>
    /// Pre-computed count of members whose level is at or above
    /// <see cref="EnvelopeMemberResolver.EncryptionThreshold"/>.
    /// Avoids recomputing via LINQ on every Decrypt call.
    /// </summary>
    public readonly System.Int32 EncryptableCount = encryptableCount;

    /// <summary>
    /// Returns <see langword="true"/> when the type has at least one sensitive member.
    /// </summary>
    public readonly System.Boolean HasAnyMembers = properties.Length > 0 || fields.Length > 0;
}
