using Nalix.Framework.Time;

namespace Nalix.Network.Listeners.Core;

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
    System.Threading.Tasks.Task StartListeningAsync(System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the listening process.
    /// This method should gracefully stop the listener, cleaning up resources and terminating any ongoing network connection acceptances.
    /// </summary>
    void StopListening();

    /// <summary>
    /// Updates the listener with the current server time, provided as a Unix timestamp.
    /// </summary>
    /// <param name="milliseconds">The current server time in milliseconds since the Unix epoch (January 1, 2020, 00:00:00 UTC), as provided by <see cref="Clock.UnixMillisecondsNow"/>.</param>
    void SynchronizeTime(System.Int64 milliseconds);
}
