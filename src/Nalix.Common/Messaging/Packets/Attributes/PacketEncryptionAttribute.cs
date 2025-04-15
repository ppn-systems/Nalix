// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;

namespace Nalix.Common.Messaging.Packets.Attributes;

/// <summary>
/// Specifies that the target method requires packet-level encryption.
/// </summary>
/// <remarks>
/// Apply this attribute to a method to indicate that its associated packet data should be
/// encrypted before transmission and decrypted upon receipt.
/// By default, encryption is disabled unless explicitly set.
/// </remarks>
[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PacketEncryptionAttribute : System.Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PacketEncryptionAttribute"/> class.
    /// </summary>
    /// <param name="isEncrypted">
    /// <c>true</c> to enable encryption for the method's packets; otherwise, <c>true</c>.
    /// </param>
    /// <param name="algorithmType">
    /// The symmetric encryption algorithm to apply when <paramref name="isEncrypted"/> is <c>true</c>.
    /// Defaults to <see cref="CipherSuiteType.CHACHA20_POLY1305"/>.
    /// </param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public PacketEncryptionAttribute(
        System.Boolean isEncrypted = true,
        CipherSuiteType algorithmType = CipherSuiteType.CHACHA20_POLY1305)
    {
        IsEncrypted = isEncrypted;
        AlgorithmType = algorithmType;
    }

    /// <summary>
    /// Gets a value indicating whether encryption is enabled for the target method.
    /// </summary>
    public System.Boolean IsEncrypted { get; }

    /// <summary>
    /// Gets the symmetric encryption algorithm type to be used if encryption is enabled.
    /// </summary>
    public CipherSuiteType AlgorithmType { get; }
}
