using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Notio.Http.Configuration;
using Notio.Http.Cookie;
using Notio.Http.Enums;
using Notio.Http.Extensions;
using Notio.Http.Interfaces;
using Notio.Http.Utils;

namespace Notio.Http.Model
{
    /// <inheritdoc />
    public class Request : IRequest
    {
        private INotioClient _client;
        private CookieJar _jar;

        /// <summary>
        /// Initializes a new instance of the <see cref="Request"/> class.
        /// </summary>
        /// <param name="url">The URL to call with this Request instance.</param>
        public Request(Url url = null)
        {
            Url = url;
        }

        /// <summary>
        /// Used internally by NotioClient.Request
        /// </summary>
        internal Request(INotioClient client, params object[] urlSegments) : this(client?.BaseUrl, urlSegments)
        {
            Client = client;
        }

        /// <summary>
        /// Used internally by NotioClient.Request and CookieSession.Request
        /// </summary>
        internal Request(string baseUrl, params object[] urlSegments)
        {
            var parts = new List<string>(urlSegments.Select(s => s.ToInvariantString()));
            if (!Url.IsValid(parts.FirstOrDefault()) && !string.IsNullOrEmpty(baseUrl))
                parts.Insert(0, baseUrl);

            if (parts.Any())
                Url = Url.Combine(parts.ToArray());
        }

        /// <inheritdoc />
        public HttpSettings Settings { get; } = new();

        /// <inheritdoc />
        public IList<(HttpEventType, INotioEventHandler)> EventHandlers { get; } = new List<(HttpEventType, INotioEventHandler)>();

        /// <inheritdoc />
        public INotioClient Client
        {
            get => _client;
            set
            {
                _client = value;
                Settings.Parent = _client?.Settings;
                SyncBaseUrl(_client, this);
                SyncHeaders(_client, this);
            }
        }

        /// <inheritdoc />
        public HttpMethod Verb { get; set; }

        /// <inheritdoc />
        public Url Url { get; set; }

        /// <inheritdoc />
        public HttpContent Content { get; set; }

        /// <inheritdoc />
        public NotioCall RedirectedFrom { get; set; }

        /// <inheritdoc />
        public INameValueList<string> Headers { get; } = new NameValueList<string>(false); // header names are case-insensitive https://stackoverflow.com/a/5259004/62600

        /// <inheritdoc />
        public IEnumerable<(string Name, string Value)> Cookies =>
            CookieCutter.ParseRequestHeader(Headers.FirstOrDefault("Cookie"));

        /// <inheritdoc />
        public CookieJar CookieJar
        {
            get => _jar;
            set => ApplyCookieJar(value);
        }

        /// <inheritdoc />
        public INotioClient EnsureClient() => Client ??= NotioHttp.GetClientForRequest(this);

        /// <inheritdoc />
        public Task<IResponse> SendAsync(HttpMethod verb, HttpContent content = null, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
            Verb = verb;
            Content = content;
            return EnsureClient().SendAsync(this, completionOption, cancellationToken);
        }

        internal static void SyncHeaders(INotioClient client, IRequest request)
        {
            if (client == null || request == null) return;

            foreach (var header in client.Headers.ToList())
            {
                if (!request.Headers.Contains(header.Name))
                    request.Headers.Add(header.Name, header.Value);
            }
        }

        /// <summary>
        /// Prepends client.BaseUrl to this.Url, but only if this.Url isn't already a valid, absolute URL.
        /// </summary>
        private static void SyncBaseUrl(INotioClient client, IRequest request)
        {
            if (string.IsNullOrEmpty(client?.BaseUrl))
                return;

            if (request.Url == null)
                request.Url = client.BaseUrl;
            else if (!Url.IsValid(request.Url))
                request.Url = Url.Combine(client.BaseUrl, request.Url);
        }

        private void ApplyCookieJar(CookieJar jar)
        {
            _jar = jar;
            if (jar == null)
                return;

            this.WithCookies(
                from c in CookieJar
                where c.ShouldSendTo(Url, out _)
                // sort by longest path, then earliest creation time, per #2: https://tools.ietf.org/html/rfc6265#section-5.4
                orderby (c.Path ?? c.OriginUrl.Path).Length descending, c.DateReceived
                select (c.Name, c.Value));
        }
    }
}