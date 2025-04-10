using System.Threading;

namespace Notio.Network.Core;

/// <summary>
/// Interface for network listener classes.
/// This interface is intended to be implemented by classes that listen for network connections
/// and handle the initiation and termination of connection listening.
/// </summary>
public interface IListener
{
    /// <summary>
    /// Starts listening for network connections using a CancellationToken for optional cancellation.
    /// This method should begin the process of accepting incoming network connections.
    /// The listening process can be cancelled via the provided CancellationToken.
    /// </summary>
    /// <param name="cancellationToken">A CancellationToken used to cancel the listening process.</param>
    void BeginListening(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the listening process.
    /// This method should gracefully stop the listener, cleaning up resources and terminating any ongoing network connection acceptances.
    /// </summary>
    void EndListening();
}
