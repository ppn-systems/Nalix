using Nalix.Common.Cryptography;
using Nalix.Shared.Configuration.Attributes;
using Nalix.Shared.Configuration.Binding;

namespace Nalix.Shared.Clients;

/// <summary>
/// Represents the configuration settings for establishing a network connection.
/// </summary>
public sealed class RemoteEndpointConfig : ConfigurationLoader
{
    private System.Byte[] _key = new System.Byte[32];

    /// <summary>
    /// Gets or sets the port number for the connection.
    /// Default value is 52006.
    /// </summary>
    public System.UInt16 Port { get; set; } = 57206;

    /// <summary>
    /// Gets or sets the server address or hostname.
    /// Default value is "0.0.0.0".
    /// </summary>
    public System.String Address { get; set; } = "0.0.0.0";

    /// <summary>
    /// Gets or sets the encryption key used for secure communication.
    /// Default value is an empty byte array.
    /// </summary>
    [ConfiguredIgnore]
    public System.Byte[] EncryptionKey
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _key;

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        set
        {
            if (value is null || value.Length != 32)
                throw new System.ArgumentException(
                    "EncryptionKey must be exactly 32 bytes.", nameof(value));

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
