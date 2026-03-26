// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using Nalix.Common.Security;

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Specifies that the target method requires packet-level encryption.
/// </summary>
/// <remarks>
/// Apply this attribute to a method to indicate that its associated packet data should be
/// encrypted before transmission and decrypted upon receipt.
/// By default, encryption is disabled unless explicitly set.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PacketEncryptionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketEncryptionAttribute"/> class.
    /// </summary>
    /// <param name="isEncrypted">
    /// <c>true</c> to enable encryption for the method's packets; otherwise, <c>true</c>.
    /// </param>
    /// <param name="algorithmType">
    /// The symmetric encryption algorithm to apply when <paramref name="isEncrypted"/> is <c>true</c>.
    /// Defaults to <see cref="CipherSuiteType.Chacha20Poly1305"/>.
    /// </param>
    [SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public PacketEncryptionAttribute(
        bool isEncrypted = true,
        CipherSuiteType algorithmType = CipherSuiteType.Chacha20Poly1305)
    {
        this.IsEncrypted = isEncrypted;
        this.AlgorithmType = algorithmType;
    }

    /// <summary>
    /// Gets a value indicating whether encryption is enabled for the target method.
    /// </summary>
    public bool IsEncrypted { get; }

    /// <summary>
    /// Gets the symmetric encryption algorithm type to be used if encryption is enabled.
    /// </summary>
    public CipherSuiteType AlgorithmType { get; }
}
