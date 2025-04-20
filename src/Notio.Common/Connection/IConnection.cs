using Notio.Common.Compression;
using Notio.Common.Cryptography;
using Notio.Common.Identity;
using Notio.Common.Security;

namespace Notio.Common.Connection;

/// <summary>
/// Represents an interface for managing a network connection.
/// </summary>
public partial interface IConnection : System.IDisposable
{
    /// <summary>
    /// Gets the unique identifier for the connection.
    /// </summary>
    IEncodedId Id { get; }

    /// <summary>
    /// Gets the total duration (in milliseconds) since the connection was established.
    /// Useful for measuring connection lifetime or session activity.
    /// </summary>
    long UpTime { get; }

    /// <summary>
    /// Gets the ping time (round-trip time) for the connection, measured in milliseconds.
    /// This value can help determine the latency of the network connection.
    /// </summary>
    long LastPingTime { get; }

    /// <summary>
    /// Gets the incoming packet of data.
    /// </summary>
    System.ReadOnlyMemory<byte> IncomingPacket { get; }

    /// <summary>
    /// Gets the remote endpoint address associated with the connection.
    /// </summary>
    string RemoteEndPoint { get; }

    /// <summary>
    /// Gets the timestamp indicating when the connection was established.
    /// </summary>
    System.DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the encryption key used for securing communication.
    /// </summary>
    byte[] EncryptionKey { get; set; }

    /// <summary>
    /// Gets or sets the encryption mode used.
    /// </summary>
    EncryptionMode EncMode { get; set; }

    /// <summary>
    /// Gets or sets the compression mode used.
    /// </summary>
    CompressionType ComMode { get; set; }

    /// <summary>
    /// Gets the authority levels associated with the connection.
    /// </summary>
    PermissionLevel Level { get; set; }

    /// <summary>
    /// Gets the current state of the connection.
    /// </summary>
    AuthenticationState State { get; set; }

    /// <summary>
    /// A dictionary for storing connection-specific metadata.
    /// This allows dynamically attaching and retrieving additional information related to the connection.
    /// </summary>
    System.Collections.Generic.Dictionary<string, object> Metadata { get; }

    /// <summary>
    /// Occurs when the connection is closed.
    /// </summary>
    event System.EventHandler<IConnectEventArgs> OnCloseEvent;

    /// <summary>
    /// Occurs when data is received and processed.
    /// </summary>
    event System.EventHandler<IConnectEventArgs> OnProcessEvent;

    /// <summary>
    /// Occurs after data has been successfully processed.
    /// </summary>
    event System.EventHandler<IConnectEventArgs> OnPostProcessEvent;

    /// <summary>
    /// Closes the connection and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// Ensures that both the socket and associated streams are properly closed.
    /// </remarks>
    void Close(bool force = false);

    /// <summary>
    /// Disconnects the connection safely with an optional reason.
    /// </summary>
    /// <param name="reason">An optional string providing the reason for disconnection.</param>
    /// <remarks>
    /// Use this method to terminate the connection gracefully.
    /// </remarks>
    void Disconnect(string reason = null);
}
