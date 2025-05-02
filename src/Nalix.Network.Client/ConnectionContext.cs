using Nalix.Common.Cryptography;
using Nalix.Shared.Configuration.Attributes;
using Nalix.Shared.Configuration.Binding;

namespace Nalix.Network.Client;

/// <summary>
/// Represents the configuration settings for establishing a network connection.
/// </summary>
public class ConnectionContext : ConfigurationBinder
{
    /// <summary>
    /// Gets or sets the server address or hostname.
    /// Default value is "127.0.0.1".
    /// </summary>
    public string ServerAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the port number for the connection.
    /// Default value is 7777.
    /// </summary>
    public int PortNumber { get; set; } = 7777;

    /// <summary>
    /// Gets or sets the encryption key used for secure communication.
    /// Default value is an empty byte array.
    /// </summary>
    [ConfiguredIgnore]
    public byte[] EncryptionKey { get; set; } = [];

    /// <summary>
    /// Gets or sets the encryption mode for the connection.
    /// This property is ignored during configuration binding.
    /// </summary>
    [ConfiguredIgnore]
    public EncryptionType EncryptionMode { get; set; }
}
