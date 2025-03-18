using Notio.Network.Web.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;
using Notio.Extensions;

namespace Notio.Network.Web;

/// <summary>
/// Provides extension methods for types implementing <see cref="IWebServer"/>.
/// </summary>
public static partial class WebServerExtensions
{
    /// <summary>
    /// Starts a web server by calling <see cref="IWebServer.RunAsync"/>
    /// in another thread.
    /// </summary>
    /// <param name="this">The <see cref="IWebServer"/> on which this method is called.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to stop the web server.</param>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The web server has already been started.</exception>
    public static void Start(this IWebServer @this, CancellationToken cancellationToken = default)
    {
        _ = Task.Run(() => @this.RunAsync(cancellationToken), cancellationToken);
        while (@this.State < WebServerState.Listening)
        {
            Task.Delay(1, cancellationToken).Await();
        }
    }
}