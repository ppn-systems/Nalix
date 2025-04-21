using Notio.Common.Compression;
using Notio.Common.Cryptography;

namespace Notio.Network.Dispatch.Dto;

/// <summary>
/// Represents the status of a network connection, including compression and encryption details.
/// </summary>
public class ConnInfoDto
{
    /// <summary>
    /// Gets the encryption mode used for the connection.
    /// </summary>
    public EncryptionType Encryption { get; init; }

    /// <summary>
    /// Gets the compression mode used for the connection.
    /// </summary>
    public CompressionType Compression { get; init; }
}
