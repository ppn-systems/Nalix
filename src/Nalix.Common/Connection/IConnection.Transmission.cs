namespace Nalix.Common.Connection;

public partial interface IConnection
{
    /// <summary>
    /// Gets the Transmission Control Protocol (TCP) transmission interface
    /// </summary>
    ITcp Tcp { get; }

    /// <summary>
    /// Gets the User Datagram Protocol (UDP) transmission interface
    /// </summary>
    IUdp Udp { get; }

    /// <summary>
    /// Represents the Transmission Control Protocol (TCP) transmission interface
    /// for a network connection.
    /// </summary>
    /// <remarks>
    /// This interface inherits from <see cref="ITransport"/> which defines
    /// common send methods for sending packets or raw data synchronously
    /// and asynchronously.
    ///
    /// TCP is a connectionless protocol, so this interface focuses mainly
    /// on sending data without connection state management or receive control.
    /// </remarks>
    public interface ITcp : ITransport
    {
        /// <summary>
        /// Starts receiving data from the connection.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token to cancel the receiving operation.
        /// </param>
        /// <remarks>
        /// Call this method to initiate listening for incoming data on the connection.
        /// </remarks>
        void BeginReceive(System.Threading.CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents the User Datagram Protocol (UDP) transmission interface
    /// for a network connection.
    /// </summary>
    /// <remarks>
    /// This interface inherits from <see cref="ITransport"/> which defines
    /// common send methods for sending packets or raw data synchronously
    /// and asynchronously.
    ///
    /// UDP is a connectionless protocol, so this interface focuses mainly
    /// on sending data without connection state management or receive control.
    /// </remarks>
    public interface IUdp : ITransport
    {
        // (Additional UDP-specific members can be added here if needed)
    }
}
