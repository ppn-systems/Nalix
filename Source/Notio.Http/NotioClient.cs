using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using Notio.Http.Utils;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Reflection;
using Notio.Http.Interfaces;
using Notio.Http.Configuration;
using Notio.Http.Testing;
using Notio.Http.Extensions;
using Notio.Http.Exceptions;
using Notio.Http.Enums;
using Notio.Http.Model;

namespace Notio.Http;

/// <summary>
/// A reusable object for making HTTP calls.
/// </summary>
public class NotioClient : INotioClient
{
    private static readonly Lazy<IClientFactory> _defaultFactory = new(() => new DefaultClientFactory());

    /// <summary>
    /// Creates a new instance of <see cref="NotioClient"/>.
    /// </summary>
    /// <param name="baseUrl">The base URL associated with this client.</param>
    public NotioClient(string baseUrl = null) : this(_defaultFactory.Value.CreateHttpClient(), baseUrl) { }

    /// <summary>
    /// Creates a new instance of <see cref="NotioClient"/>, wrapping an existing HttpClient.
    /// Generally, you should let Flurl create and manage HttpClient instances for you, but you might, for
    /// example, have an HttpClient instance that was created by a 3rd-party library and you want to use
    /// Flurl to build and send calls with it. Be aware that if the HttpClient has an underlying
    /// HttpMessageHandler that processes cookies and automatic redirects (as is the case by default),
    /// Flurl's re-implementation of those features may not work properly.
    /// </summary>
    /// <param name="httpClient">The instantiated HttpClient instance.</param>
    /// <param name="baseUrl">Optional. The base URL associated with this client.</param>
    public NotioClient(HttpClient httpClient, string baseUrl = null) : this(httpClient, baseUrl, null, null, null) { }

    // ClientBuilder gets some special privileges
    internal NotioClient(HttpClient httpClient, string baseUrl, HttpSettings settings, INameValueList<string> headers, IList<(HttpEventType, INotioEventHandler)> eventHandlers)
    {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        BaseUrl = baseUrl ?? httpClient.BaseAddress?.ToString();

        Settings = settings ?? new HttpSettings { Timeout = httpClient.Timeout };
        // Timeout can be overridden per request, so don't constrain it by the underlying HttpClient
        httpClient.Timeout = Timeout.InfiniteTimeSpan;

        EventHandlers = eventHandlers ?? new List<(HttpEventType, INotioEventHandler)>();

        Headers = headers ?? new NameValueList<string>(false); // header names are case-insensitive https://stackoverflow.com/a/5259004/62600

        foreach (var header in GetHeadersFromHttpClient(httpClient))
        {
            if (!Headers.Contains(header.Name))
                Headers.Add(header);
        }
    }

    // reflection is (relatively) expensive, so keep a cache of HttpRequestHeaders properties
    // https://learn.microsoft.com/en-us/dotnet/api/system.net.http.headers.httprequestheaders?#properties
    private static IDictionary<string, PropertyInfo> _reqHeaderProps =
        typeof(HttpRequestHeaders).GetProperties().ToDictionary(p => p.Name.ToLower(), p => p);

    private static IEnumerable<(string Name, string Value)> GetHeadersFromHttpClient(HttpClient httpClient)
    {
        foreach (var h in httpClient.DefaultRequestHeaders)
        {
            // MS isn't making this easy. In some cases, a header value will be split into multiple values, but when iterating the collection
            // there's no way to know exactly how to piece them back together. The standard says multiple values should be comma-delimited,
            // but with User-Agent they need to be space-delimited. ToString() on properties like UserAgent do this correctly though, so when spinning
            // through the collection we'll try to match the header name to a property and ToString() it, otherwise we'll comma-delimit the values.
            if (_reqHeaderProps.TryGetValue(h.Key.Replace("-", "").ToLower(), out var prop))
            {
                var val = prop.GetValue(httpClient.DefaultRequestHeaders).ToString();
                yield return (h.Key, val);
            }
            else
            {
                yield return (h.Key, string.Join(",", h.Value));
            }
        }
    }

    /// <inheritdoc />
    public string BaseUrl { get; set; }

    /// <inheritdoc />
    public HttpSettings Settings { get; }

    /// <inheritdoc />
    public IList<(HttpEventType, INotioEventHandler)> EventHandlers { get; }

    /// <inheritdoc />
    public INameValueList<string> Headers { get; }

    /// <inheritdoc />
    public HttpClient HttpClient { get; }

    /// <inheritdoc />
    public IRequest Request(params object[] urlSegments) => new Request(this, urlSegments);

    /// <inheritdoc />
    public async Task<IResponse> SendAsync(IRequest request, 
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Url == null)
            throw new ArgumentException("Cannot send Request. Url property was not set.");
        if (!Url.IsValid(request.Url))
            throw new ArgumentException($"Cannot send Request. {request.Url} is a not a valid URL.");

        var settings = request.Settings;
        var reqMsg = new HttpRequestMessage(request.Verb, request.Url)
        {
            Content = request.Content,
            Version = Version.Parse(settings.HttpVersion)
        };

        SyncHeaders(request, reqMsg);
        var call = new NotioCall
        {
            Client = this,
            Request = request,
            HttpRequestMessage = reqMsg
        };

        await RaiseEventAsync(HttpEventType.BeforeCall, call).ConfigureAwait(false);

        // in case URL or headers were modified in the handler above
        reqMsg.RequestUri = request.Url.ToUri();
        SyncHeaders(request, reqMsg);

        call.StartedUtc = DateTime.UtcNow;
        var ct = GetCancellationTokenWithTimeout(cancellationToken, settings.Timeout, out var cts);

        HttpTest.Current?.LogCall(call);

        try
        {
            call.HttpResponseMessage =
                HttpTest.Current?.FindSetup(call)?.GetNextResponse() ??
                await HttpClient.SendAsync(reqMsg, completionOption, ct).ConfigureAwait(false);

            call.HttpResponseMessage.RequestMessage = reqMsg;
            call.Response = new Response(call, request.CookieJar);

            if (call.Succeeded)
            {
                var redirResponse = await ProcessRedirectAsync(call, completionOption, cancellationToken).ConfigureAwait(false);
                return redirResponse ?? call.Response;
            }
            else
                throw new HttpException(call, null);
        }
        catch (Exception ex)
        {
            return await HandleExceptionAsync(call, ex, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            cts?.Dispose();
            call.EndedUtc = DateTime.UtcNow;
            await RaiseEventAsync(HttpEventType.AfterCall, call).ConfigureAwait(false);
        }
    }

    private void SyncHeaders(IRequest req, HttpRequestMessage reqMsg)
    {
        // copy any client-level (default) headers to Request
        Model.Request.SyncHeaders(this, req);

        // copy headers from Request to HttpRequestMessage
        foreach (var (Name, Value) in req.Headers)
            reqMsg.SetHeader(Name, Value.Trim(), false);

        if (reqMsg.Content == null)
            return;

        // copy headers from HttpContent to Request
        foreach (var header in reqMsg.Content.Headers.ToList())
        {
            if (!req.Headers.Contains(header.Key))
                req.Headers.AddOrReplace(header.Key, string.Join(",", header.Value));
        }
    }

    private async Task<IResponse> ProcessRedirectAsync(NotioCall call, HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        var settings = call.Request.Settings;
        if (settings.Redirects.Enabled)
            call.Redirect = GetRedirect(call);

        if (call.Redirect == null)
            return null;

        await RaiseEventAsync(HttpEventType.OnRedirect, call).ConfigureAwait(false);

        if (call.Redirect.Follow != true)
            return null;

        var changeToGet = call.Redirect.ChangeVerbToGet;

        var redir = new Request(this)
        {
            Url = call.Redirect.Url,
            Verb = changeToGet ? HttpMethod.Get : call.HttpRequestMessage.Method,
            Content = changeToGet ? null : call.Request.Content,
            RedirectedFrom = call,
            Settings = { Parent = settings }
        };

        foreach (var handler in call.Request.EventHandlers)
            redir.EventHandlers.Add(handler);

        if (call.Request.CookieJar != null)
            redir.CookieJar = call.Request.CookieJar;

        redir.WithHeaders(call.Request.Headers.Where(h =>
            h.Name.OrdinalEquals("Cookie", true) ? false : // never blindly forward Cookie header; CookieJar should be used to ensure rules are enforced
            h.Name.OrdinalEquals("Authorization", true) ? settings.Redirects.ForwardAuthorizationHeader :
            h.Name.OrdinalEquals("Transfer-Encoding", true) ? settings.Redirects.ForwardHeaders && !changeToGet :
            settings.Redirects.ForwardHeaders));

        var ct = GetCancellationTokenWithTimeout(cancellationToken, settings.Timeout, out var cts);
        try
        {
            return await SendAsync(redir, completionOption, ct).ConfigureAwait(false);
        }
        finally
        {
            cts?.Dispose();
        }
    }

    // partially lifted from https://github.com/dotnet/runtime/blob/master/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/RedirectHandler.cs
    private static NotioRedirect GetRedirect(NotioCall call)
    {
        if (call.Response.StatusCode < 300 || call.Response.StatusCode > 399)
            return null;

        if (!call.Response.Headers.TryGetFirst("Location", out var location))
            return null;

        var redir = new NotioRedirect();

        if (Url.IsValid(location))
            redir.Url = new Url(location);
        else if (location.OrdinalStartsWith("//"))
            redir.Url = new Url(call.Request.Url.Scheme + ":" + location);
        else if (location.OrdinalStartsWith("/"))
            redir.Url = Url.Combine(call.Request.Url.Root, location);
        else
            redir.Url = Url.Combine(call.Request.Url.Root, call.Request.Url.Path, location);

        // Per https://tools.ietf.org/html/rfc7231#section-7.1.2, a redirect location without a
        // fragment should inherit the fragment from the original URI.
        if (string.IsNullOrEmpty(redir.Url.Fragment))
            redir.Url.Fragment = call.Request.Url.Fragment;

        redir.Count = 1 + (call.Request.RedirectedFrom?.Redirect?.Count ?? 0);

        var isSecureToInsecure = call.Request.Url.IsSecureScheme && !redir.Url.IsSecureScheme;

        redir.Follow =
            new[] { 301, 302, 303, 307, 308 }.Contains(call.Response.StatusCode) &&
            redir.Count <= call.Request.Settings.Redirects.MaxAutoRedirects &&
            (call.Request.Settings.Redirects.AllowSecureToInsecure || !isSecureToInsecure);

        bool ChangeVerbToGetOn(int statusCode, HttpMethod verb)
        {
            switch (statusCode)
            {
                // 301 and 302 are a bit ambiguous. The spec says to preserve the verb
                // but most browsers rewrite it to GET. HttpClient stack changes it if
                // only it's a POST, presumably since that's a browser-friendly verb.
                // Seems odd, but sticking with that is probably the safest bet.
                // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/RedirectHandler.cs#L140
                case 301:
                case 302:
                    return verb == HttpMethod.Post;
                case 303:
                    return true;
                default: // 307 & 308 mainly
                    return false;
            }
        }

        redir.ChangeVerbToGet =
            redir.Follow &&
            ChangeVerbToGetOn(call.Response.StatusCode, call.Request.Verb);

        return redir;
    }

    internal static async Task RaiseEventAsync(HttpEventType eventType, NotioCall call)
    {
        // client-level handlers first, then request-level
        var handlers = call.Client.EventHandlers
            .Concat(call.Request.EventHandlers)
            .Where(h => h.EventType == eventType)
            .Select(h => h.Handler)
            .ToList();

        foreach (var handler in handlers)
        {
            // sync first, then async
            handler.Handle(eventType, call);
            await handler.HandleAsync(eventType, call);
        }
    }

    internal static async Task<IResponse> HandleExceptionAsync(NotioCall call, Exception ex, CancellationToken token)
    {
        call.Exception = ex;
        await RaiseEventAsync(HttpEventType.OnError, call).ConfigureAwait(false);

        if (call.ExceptionHandled)
            return call.Response;

        if (ex is OperationCanceledException && !token.IsCancellationRequested)
            throw new HttpTimeoutException(call, ex);

        if (ex is HttpException)
            throw ex;

        throw new HttpException(call, ex);
    }

    private static CancellationToken GetCancellationTokenWithTimeout(CancellationToken original, TimeSpan? timeout, out CancellationTokenSource timeoutTokenSource)
    {
        timeoutTokenSource = null;
        if (!timeout.HasValue)
            return original;

        timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(original);
        timeoutTokenSource.CancelAfter(timeout.Value);
        return timeoutTokenSource.Token;
    }

    /// <inheritdoc />
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Disposes the underlying HttpClient and HttpMessageHandler.
    /// </summary>
    public virtual void Dispose()
    {
        if (IsDisposed)
            return;

        HttpClient.Dispose();
        IsDisposed = true;
    }
}