using Notio.Http.Cookie;
using Notio.Http.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Http.Interfaces;

/// <summary>
/// Represents an HTTP request. Can be created explicitly via new Request(), fluently via Url.Request(),
/// or implicitly when a call is made via methods like Url.GetAsync().
/// </summary>
public interface IRequest : ISettingsContainer, IHeadersContainer, IEventHandlerContainer
{
    /// <summary>
    /// Gets or sets the INotioClient to use when sending the request.
    /// </summary>
    INotioClient Client { get; set; }

    /// <summary>
    /// Gets or sets the HTTP method of the request. Normally you don't need to set this explicitly; it will be set
    /// when you call the sending method, such as GetAsync, PostAsync, etc.
    /// </summary>
    HttpMethod Verb { get; set; }

    /// <summary>
    /// Gets or sets the URL to be called.
    /// </summary>
    Url Url { get; set; }

    /// <summary>
    /// The body content of this request.
    /// </summary>
    HttpContent Content { get; set; }

    /// <summary>
    /// Gets Name/Value pairs parsed from the Cookie request header.
    /// </summary>
    IEnumerable<(string Name, string Value)> Cookies { get; }

    /// <summary>
    /// Gets or sets the collection of HTTP cookies that can be shared between multiple requests. When set, values that
    /// should be sent with this request (based on Domain, Path, and other rules) are immediately copied to the Cookie
    /// request header, and any Set-Cookie headers received in the response will be written to the CookieJar.
    /// </summary>
    CookieJar CookieJar { get; set; }

    /// <summary>
    /// The NotioCall that received a 3xx response and automatically triggered this request.
    /// </summary>
    NotioCall RedirectedFrom { get; set; }

    /// <summary>
    /// If NotioClient property is null, selects (or creates) a NotioClient from the global NotioHttp.Clients cache. Called
    /// automatically just before a request is sent, so in most cases there is no need to call explicitly.
    /// </summary>
    INotioClient EnsureClient();

    /// <summary>
    /// Asynchronously sends the HTTP request. Mainly used to implement higher-level extension methods (GetJsonAsync, etc).
    /// </summary>
    /// <param name="verb">The HTTP method used to make the request.</param>
    /// <param name="content">Contents of the request body.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <param name="completionOption">The HttpCompletionOption used in the request. Optional.</param>
    /// <returns>A Task whose result is the received IResponse.</returns>
    Task<IResponse> SendAsync(HttpMethod verb, HttpContent content = null, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default);
}
