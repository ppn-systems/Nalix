using System.Threading;

namespace Notio.Network.Listeners;

/// <summary>
/// Interface for network listener classes.
/// </summary>
internal interface IListener
{
    /// <summary>
    /// Starts listening for network connections using a CancellationToken for optional cancellation.
    /// </summary>
    /// <param name="cancellationToken">A CancellationToken used to cancel the listening process.</param>
    void BeginListening(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the listening process.
    /// </summary>
    void EndListening();
}