using Notio.Http.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Http.Interfaces;

/// <summary>
/// Interface defining NotioClient's contract (useful for mocking and DI)
/// </summary>
public interface INotioClient : ISettingsContainer, IHeadersContainer, IEventHandlerContainer, IDisposable
{
    /// <summary>
    /// Gets the HttpClient that this INotioClient wraps.
    /// </summary>
    HttpClient HttpClient { get; }

    /// <summary>
    /// Gets or sets the base URL used for all calls made with this client.
    /// </summary>
    string BaseUrl { get; set; }

    /// <summary>
    /// Creates a new IRequest that can be further built and sent fluently.
    /// </summary>
    /// <param name="urlSegments">The URL or URL segments for the request. If BaseUrl is defined, it is assumed that these are path segments off that base.</param>
    /// <returns>A new IRequest</returns>
    IRequest Request(params object[] urlSegments);

    /// <summary>
    /// Gets a value indicating whether this instance (and its underlying HttpClient) has been disposed.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// Asynchronously sends an HTTP request.
    /// </summary>
    /// <param name="request">The IRequest to send.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <param name="completionOption">The HttpCompletionOption used in the request. Optional.</param>
    /// <returns>A Task whose result is the received IResponse.</returns>
    Task<IResponse> SendAsync(IRequest request, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default);
}
