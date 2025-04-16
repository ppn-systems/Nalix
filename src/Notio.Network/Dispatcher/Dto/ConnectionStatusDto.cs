using Notio.Common.Cryptography;
using Notio.Common.Security;

namespace Notio.Network.Dispatcher.Dto;

/// <summary>
/// Represents the status of a network connection, including compression and encryption details.
/// </summary>
public class ConnectionStatusDto
{
    /// <summary>
    /// Gets the encryption mode used for the connection.
    /// </summary>
    public EncryptionMode EncMode { get; init; }

    /// <summary>
    /// Gets the compression mode used for the connection.
    /// </summary>
    public CompressionMode ComMode { get; init; }
}
