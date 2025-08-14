// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Security.Types;

/// <summary>
/// Specifies the types of security guards that can be applied to protect resources or operations.
/// </summary>
public enum GuardType : System.Byte
{
    /// <summary>
    /// No security guard is applied.
    /// </summary>
    None = 0x00,

    /// <summary>
    /// Access requires the user to be authenticated.
    /// </summary>
    Authenticated = 0xA7,

    /// <summary>
    /// Access requires a valid session to be present.
    /// </summary>
    SessionValid = 0x1C,

    /// <summary>
    /// Enforces rate limiting to prevent excessive or abusive requests.
    /// </summary>
    RateLimited = 0xF3,

    /// <summary>
    /// Access requires the request to originate from a trusted IP address.
    /// </summary>
    IpTrusted = 0x8B,

    /// <summary>
    /// Prevents replay attacks by ensuring each request is unique.
    /// </summary>
    NoReplay = 0xD4,

    /// <summary>
    /// Requires specific permissions to access the resource or perform the operation.
    /// </summary>
    PermissionRequired = 0x2E,

    /// <summary>
    /// Verifies payload integrity through a valid cryptographic signature.
    /// </summary>
    PayloadSignatureVerified = 0x9A,

    /// <summary>
    /// Access requires a completed handshake process.
    /// </summary>
    HandshakeComplete = 0x65,

    /// <summary>
    /// Restricts access to encrypted communication channels only.
    /// </summary>
    EncryptedChannelOnly = 0xC1
}
