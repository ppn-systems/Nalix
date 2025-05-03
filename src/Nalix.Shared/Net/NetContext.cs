using Nalix.Common.Cryptography;
using Nalix.Shared.Configuration.Attributes;
using Nalix.Shared.Configuration.Binding;
using System;
using System.Runtime.CompilerServices;

namespace Nalix.Shared.Net;

/// <summary>
/// Represents the configuration settings for establishing a network connection.
/// </summary>
public sealed class NetContext : ConfigurationLoader
{
    private byte[] _key = new byte[32];

    /// <summary>
    /// Gets or sets the port number for the connection.
    /// Default value is 7777.
    /// </summary>
    public int Port { get; set; } = 7777;

    /// <summary>
    /// Gets or sets the server address or hostname.
    /// Default value is "127.0.0.1".
    /// </summary>
    public string Address { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the encryption key used for secure communication.
    /// Default value is an empty byte array.
    /// </summary>
    [ConfiguredIgnore]
    public byte[] EncryptionKey
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _key;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (value is null || value.Length != 32)
                throw new ArgumentException("EncryptionKey must be exactly 32 bytes.", nameof(value));
            _key = value;
        }
    }

    /// <summary>
    /// Gets or sets the encryption mode for the connection.
    /// This property is ignored during configuration binding.
    /// </summary>
    [ConfiguredIgnore]
    public EncryptionType EncryptionMode { get; set; }
}
