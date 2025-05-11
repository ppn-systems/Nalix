namespace Nalix.Network.Listeners;

/// <summary>
/// Interface for network listener classes.
/// This interface is intended to be implemented by classes that listen for network connections
/// and handle the initiation and termination of connection listening.
/// </summary>
public interface IListener
{
    /// <summary>
    /// Updates the listener with the current server time, provided as a Unix timestamp.
    /// </summary>
    /// <param name="milliseconds">The current server time in milliseconds since the Unix epoch (January 1, 2020, 00:00:00 UTC), as provided by <see cref="Shared.Time.Clock.UnixMillisecondsNow"/>.</param>
    abstract void UpdateTime(long milliseconds);

    /// <summary>
    /// Stops the listening process.
    /// This method should gracefully stop the listener, cleaning up resources and terminating any ongoing network connection acceptances.
    /// </summary>
    void EndListening();

    /// <summary>
    /// Starts listening for network connections using a CancellationToken for optional cancellation.
    /// This method should begin the process of accepting incoming network connections.
    /// The listening process can be cancelled via the provided CancellationToken.
    /// </summary>
    /// <param name="cancellationToken">A CancellationToken used to cancel the listening process.</param>
    System.Threading.Tasks.Task BeginListeningAsync(System.Threading.CancellationToken cancellationToken);
}
