using Notio.Common.Security;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Common.Connection;

/// <summary>
/// Represents an interface for managing a network connection.
/// </summary>
public interface IConnection : IDisposable
{
    /// <summary>
    /// Gets the unique identifier for the connection.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the ping time (round-trip time) for the connection, measured in milliseconds.
    /// This value can help determine the latency of the network connection.
    /// </summary>
    long PingTime { get; }

    /// <summary>
    /// Gets the incoming packet of data.
    /// </summary>
    ReadOnlyMemory<byte> IncomingPacket { get; }

    /// <summary>
    /// Gets the remote endpoint address associated with the connection.
    /// </summary>
    string RemoteEndPoint { get; }

    /// <summary>
    /// Gets the timestamp indicating when the connection was established.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the encryption key used for securing communication.
    /// </summary>
    byte[] EncryptionKey { get; set; }

    /// <summary>
    /// Gets or sets the encryption mode used.
    /// </summary>
    public EncryptionMode Mode { get; set; }

    /// <summary>
    /// Gets the authority levels associated with the connection.
    /// </summary>
    AuthorityLevel Authority { get; set; }

    /// <summary>
    /// Gets the current state of the connection.
    /// </summary>
    ConnectionState State { get; set; }

    /// <summary>
    /// A dictionary for storing connection-specific metadata.
    /// This allows dynamically attaching and retrieving additional information related to the connection.
    /// </summary>
    Dictionary<string, object> Metadata { get; }

    /// <summary>
    /// Occurs when the connection is closed.
    /// </summary>
    event EventHandler<IConnectEventArgs> OnCloseEvent;

    /// <summary>
    /// Occurs when data is received and processed.
    /// </summary>
    event EventHandler<IConnectEventArgs> OnProcessEvent;

    /// <summary>
    /// Occurs after data has been successfully processed.
    /// </summary>
    event EventHandler<IConnectEventArgs> OnPostProcessEvent;

    /// <summary>
    /// Starts receiving data from the connection.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to cancel the receiving operation.
    /// </param>
    /// <remarks>
    /// Call this method to initiate listening for incoming data on the connection.
    /// </remarks>
    void BeginReceive(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the connection and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// Ensures that both the socket and associated streams are properly closed.
    /// </remarks>
    void Close(bool force = false);

    /// <summary>
    /// Sends a message synchronously over the connection.
    /// </summary>
    /// <param name="message">The message to send.</param>
    bool Send(Memory<byte> message);

    /// <summary>
    /// Sends a message asynchronously over the connection.
    /// </summary>
    /// <param name="message">The data to send.</param>
    /// <param name="cancellationToken">A token to cancel the sending operation.</param>
    /// <returns>A task that represents the asynchronous sending operation.</returns>
    /// <remarks>
    /// If the connection has been authenticated, the data will be encrypted before sending.
    /// </remarks>
    Task<bool> SendAsync(Memory<byte> message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects the connection safely with an optional reason.
    /// </summary>
    /// <param name="reason">An optional string providing the reason for disconnection.</param>
    /// <remarks>
    /// Use this method to terminate the connection gracefully.
    /// </remarks>
    void Disconnect(string reason = null);
}
