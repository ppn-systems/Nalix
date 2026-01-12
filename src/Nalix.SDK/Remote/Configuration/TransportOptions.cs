// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Core.Attributes;
using Nalix.Common.Core.Enums;
using Nalix.Common.Infrastructure.Client;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.SDK.Remote.Configuration;

/// <summary>
/// Represents the configuration settings for establishing a network connection.
/// </summary>
public sealed class TransportOptions : ConfigurationLoader, ITransportOptions
{
    private System.Byte[] _key = [];

    /// <summary>
    /// Gets or sets the port number for the connection.
    /// Default value is 57206.
    /// </summary>
    public System.UInt16 Port
    {
        [System.Diagnostics.DebuggerStepThrough]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get;

        [System.Diagnostics.DebuggerStepThrough]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set;
    } = 57206;

    /// <summary>
    /// Gets or sets the server address or hostname.
    /// Default value is "0.0.0.0".
    /// </summary>
    public System.String Address { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the maximum size (in bytes) allowed for incoming data packets.
    /// Default value is 3000.
    /// </summary>
    public System.Int32 IncomingSize { get; set; } = 3000;

    /// <summary>
    /// Gets or sets the encryption key used for secure communication.
    /// Default value is an empty byte array.
    /// </summary>
    [ConfiguredIgnore]
    public System.Byte[] EncryptionKey
    {
        [System.Diagnostics.DebuggerStepThrough]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _key;

        [System.Diagnostics.DebuggerStepThrough]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set
        {
            if (value is null || value.Length != 32)
            {
                throw new System.ArgumentException(
                    "EncryptionKey must be exactly 32 bytes.", nameof(value));
            }

            _key = value;
        }
    }

    /// <summary>
    /// Outbound queue size for the transport.
    /// </summary>
    public System.Int32 OutboundQueueSize { get; set; } = 512;

    /// <summary>
    /// Gets or sets the encryption mode for the connection.
    /// This property is ignored during configuration binding.
    /// </summary>
    [ConfiguredIgnore]
    public CipherSuiteType EncryptionMode { get; set; } = CipherSuiteType.CHACHA20_POLY1305;
}