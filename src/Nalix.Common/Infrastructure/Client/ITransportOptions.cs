// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;

namespace Nalix.Common.Infrastructure.Client;

/// <summary>
/// Defines the options required for configuring a transport connection, including
/// port, address, encryption key, and encryption mode.
/// </summary>
public interface ITransportOptions
{
    /// <summary>
    /// Gets the port number for the connection.
    /// </summary>
    System.UInt16 Port { get; set; }

    /// <summary>
    /// Gets the server address or hostname.
    /// </summary>
    System.String Address { get; set; }

    /// <summary>
    /// Gets the encryption key used for secure communication.
    /// Default value is an empty byte array.
    /// </summary>
    System.Byte[] EncryptionKey { get; set; }

    /// <summary>
    /// Gets the encryption mode for the connection.
    /// This property is ignored during configuration binding.
    /// </summary>
    CipherSuiteType EncryptionMode { get; set; }
}
