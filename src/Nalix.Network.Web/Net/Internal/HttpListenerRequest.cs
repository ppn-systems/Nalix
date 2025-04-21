using Nalix.Network.Web.Enums;
using Nalix.Network.Web.Http;
using Nalix.Network.Web.Internal;
using Nalix.Network.Web.Utilities;
using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Nalix.Network.Web.Net.Internal;

/// <summary>
/// Represents an HTTP Listener Request.
/// </summary>
internal sealed partial class HttpListenerRequest : IHttpRequest
{
    private static readonly byte[] HttpStatus100 = WebServer.DefaultEncoding.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
    private static readonly char[] Separators = [' '];

    private readonly HttpConnection _connection;
    private CookieList? _cookies;
    private Stream? _inputStream;
    private bool _kaSet;
    private bool _keepAlive;

    internal HttpListenerRequest(HttpListenerContext context)
        => _connection = context.Connection;

    /// <summary>
    /// Gets the MIME accept types.
    /// </summary>
    /// <value>
    /// The accept types.
    /// </value>
    public string[] AcceptTypes { get; private set; } = [];

    /// <inheritdoc />
    public Encoding ContentEncoding
    {
        get
        {
            if (!HasEntityBody || ContentType == null)
            {
                return WebServer.DefaultEncoding;
            }

            string? charSet = HeaderUtility.GetCharset(ContentType);
            if (string.IsNullOrEmpty(charSet))
            {
                return WebServer.DefaultEncoding;
            }

            try
            {
                return Encoding.GetEncoding(charSet);
            }
            catch (ArgumentException)
            {
                return WebServer.DefaultEncoding;
            }
        }
    }

    /// <inheritdoc />
    public long ContentLength64 => long.TryParse(Headers[HttpHeaderNames.ContentLength], out long val) ? val : 0;

    /// <inheritdoc />
    public string? ContentType => Headers[HttpHeaderNames.ContentType];

    /// <inheritdoc />
    public ICookieCollection Cookies => _cookies ??= [];

    /// <inheritdoc />
    public bool HasEntityBody => ContentLength64 > 0;

    /// <inheritdoc />
    public NameValueCollection Headers { get; } = [];

    /// <inheritdoc />
    public string HttpMethod { get; private set; } = string.Empty;

    /// <inheritdoc />
    public HttpVerbs HttpVerb { get; private set; }

    /// <inheritdoc />
    public Stream InputStream => _inputStream ??= ContentLength64 > 0 ? _connection.GetRequestStream(ContentLength64) : Stream.Null;

    /// <inheritdoc />
    public bool IsAuthenticated => false;

    /// <inheritdoc />
    public bool IsLocal => LocalEndPoint.Address?.Equals(RemoteEndPoint.Address) ?? true;

    /// <inheritdoc />
    public bool IsSecureConnection => _connection.IsSecure;

    /// <inheritdoc />
    public bool KeepAlive
    {
        get
        {
            if (!_kaSet)
            {
                string[]? cnc = Headers.GetValues(HttpHeaderNames.Connection);
                _keepAlive = ProtocolVersion < HttpVersion.Version11
                    ? cnc != null && cnc.Length == 1 && string.Compare(cnc[0], "keep-alive", StringComparison.OrdinalIgnoreCase) == 0
                    : cnc == null || cnc.All(s => string.Compare(s, "close", StringComparison.OrdinalIgnoreCase) != 0);

                _kaSet = true;
            }

            return _keepAlive;
        }
    }

    /// <inheritdoc />
    public IPEndPoint LocalEndPoint => _connection.LocalEndPoint;

    /// <inheritdoc />
    public Version ProtocolVersion { get; private set; } = HttpVersion.Version11;

    /// <inheritdoc />
    public NameValueCollection QueryString { get; } = [];

    /// <inheritdoc />
    public string RawUrl { get; private set; } = string.Empty;

    /// <inheritdoc />
    public IPEndPoint RemoteEndPoint => _connection.RemoteEndPoint;

    /// <inheritdoc />
    public Uri Url { get; private set; } = WebServer.NullUri;

    /// <inheritdoc />
    public Uri? UrlReferrer { get; private set; }

    /// <inheritdoc />
    public string UserAgent => Headers[HttpHeaderNames.UserAgent] ?? string.Empty;

    public string UserHostAddress => LocalEndPoint.ToString();

    public string? UserHostName => Headers[HttpHeaderNames.Host];

    public string[] UserLanguages { get; private set; } = [];

    /// <inheritdoc />
    public bool IsWebSocketRequest
        => HttpVerb == HttpVerbs.Get
        && ProtocolVersion >= HttpVersion.Version11
        && Headers.Contains(HttpHeaderNames.Upgrade, "websocket")
        && Headers.Contains(HttpHeaderNames.Connection, "Upgrade");

    internal void SetRequestLine(string req)
    {
        const string forbiddenMethodChars = "\"(),/:;<=>?@[\\]{}";

        string[] parts = req.Split(Separators, 3);
        if (parts.Length != 3)
        {
            _connection.SetError("Invalid request line (parts).");
            return;
        }

        HttpMethod = parts[0];
        foreach (char c in HttpMethod)
        {
            // See https://tools.ietf.org/html/rfc7230#section-3.2.6
            // for the list of allowed characters
            if (c < 32 || c >= 127 || forbiddenMethodChars.Contains(c))
            {
                _connection.SetError("(Invalid verb)");
                return;
            }
        }

        HttpVerb = IsKnownHttpMethod(HttpMethod, out HttpVerbs verb) ? verb : HttpVerbs.Any;

        RawUrl = parts[1];
        if (parts[2].Length != 8 || !parts[2].StartsWith("HTTP/", StringComparison.Ordinal))
        {
            _connection.SetError("Invalid request line (missing HTTP version).");
            return;
        }

        try
        {
            ProtocolVersion = new Version(parts[2][5..]);

            if (ProtocolVersion.Major < 1)
            {
                throw new InvalidOperationException();
            }
        }
        catch
        {
            _connection.SetError("Invalid request line (could not parse HTTP version).");
        }
    }

    internal void FinishInitialization()
    {
        string? host = UserHostName;
        if (ProtocolVersion > HttpVersion.Version10 && string.IsNullOrEmpty(host))
        {
            _connection.SetError("Invalid host name");
            return;
        }

        Uri? rawUri = UriUtility.StringToAbsoluteUri(RawUrl.ToLowerInvariant());
        string path = rawUri?.PathAndQuery ?? RawUrl;

        if (string.IsNullOrEmpty(host))
        {
            host = rawUri?.Host ?? UserHostAddress;
        }

        int colon = host.LastIndexOf(':');
        if (colon >= 0)
        {
            host = host[..colon];
        }

        // var baseUri = $"{(IsSecureConnection ? "https" : "http")}://{host}:{LocalEndPoint.Port}";
        string baseUri = $"http://{host}:{LocalEndPoint.Port}";

        if (!Uri.TryCreate(baseUri + path, UriKind.Absolute, out Uri? url))
        {
            _connection.SetError(WebUtility.HtmlEncode($"Invalid url: {baseUri}{path}"));
            return;
        }

        Url = url;
        InitializeQueryString(Url.Query);

        if (ContentLength64 == 0 && (HttpVerb == HttpVerbs.Post || HttpVerb == HttpVerbs.Put))
        {
            return;
        }

        if (string.Compare(Headers["Expect"], "100-continue", StringComparison.OrdinalIgnoreCase) == 0)
        {
            _connection.GetResponseStream().InternalWrite(HttpStatus100, 0, HttpStatus100.Length);
        }
    }

    internal void AddHeader(string header)
    {
        int colon = header.IndexOf(':');
        if (colon is (-1) or 0)
        {
            _connection.SetError("Bad Request");
            return;
        }

        string name = header[..colon].Trim();
        string val = header[(colon + 1)..].Trim();

        Headers.Set(name, val);

        switch (name.ToLowerInvariant())
        {
            case "accept-language":
                UserLanguages = val.SplitByComma(); // yes, only split with a ','
                break;

            case "accept":
                AcceptTypes = val.SplitByComma(); // yes, only split with a ','
                break;

            case "content-length":
                Headers[HttpHeaderNames.ContentLength] = val.Trim();

                if (ContentLength64 < 0)
                {
                    _connection.SetError("Invalid Content-Length.");
                }

                break;

            case "referer":
                try
                {
                    UrlReferrer = new Uri(val);
                }
                catch
                {
                    UrlReferrer = null;
                }

                break;

            case "cookie":
                ParseCookies(val);

                break;
        }
    }

    // returns true is the stream could be reused.
    internal bool FlushInput()
    {
        if (!HasEntityBody)
        {
            return true;
        }

        int length = 2048;
        if (ContentLength64 > 0)
        {
            length = (int)Math.Min(ContentLength64, length);
        }

        byte[] bytes = new byte[length];

        while (true)
        {
            try
            {
                if (InputStream.Read(bytes, 0, length) <= 0)
                {
                    return true;
                }
            }
            catch (ObjectDisposedException)
            {
                _inputStream = null;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    // Optimized for the following list of methods:
    // "DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"
    // ***NOTE***: The verb parameter is NOT VALID upon exit if false is returned.
    private static bool IsKnownHttpMethod(string method, out HttpVerbs verb)
    {
        switch (method.Length)
        {
            case 3:
                switch (method[0])
                {
                    case 'G':
                        verb = HttpVerbs.Get;
                        return method[1] == 'E' && method[2] == 'T';

                    case 'P':
                        verb = HttpVerbs.Put;
                        return method[1] == 'U' && method[2] == 'T';

                    default:
                        verb = HttpVerbs.Any;
                        return false;
                }

            case 4:
                switch (method[0])
                {
                    case 'H':
                        verb = HttpVerbs.Head;
                        return method[1] == 'E' && method[2] == 'A' && method[3] == 'D';

                    case 'P':
                        verb = HttpVerbs.Post;
                        return method[1] == 'O' && method[2] == 'S' && method[3] == 'T';

                    default:
                        verb = HttpVerbs.Any;
                        return false;
                }

            case 5:
                verb = HttpVerbs.Patch;
                return method[0] == 'P'
                    && method[1] == 'A'
                    && method[2] == 'T'
                    && method[3] == 'C'
                    && method[4] == 'H';

            case 6:
                verb = HttpVerbs.Delete;
                return method[0] == 'D'
                    && method[1] == 'E'
                    && method[2] == 'L'
                    && method[3] == 'E'
                    && method[4] == 'T'
                    && method[5] == 'E';

            case 7:
                verb = HttpVerbs.Options;
                return method[0] == 'O'
                    && method[1] == 'P'
                    && method[2] == 'T'
                    && method[3] == 'I'
                    && method[4] == 'O'
                    && method[5] == 'N'
                    && method[6] == 'S';

            default:
                verb = HttpVerbs.Any;
                return false;
        }
    }

    private void ParseCookies(string val)
    {
        _cookies ??= [];

        System.Collections.Generic.IEnumerable<string> cookieStrings = val.SplitByAny(';', ',')
            .Where(x => !string.IsNullOrEmpty(x));
        Cookie? current = null;
        int version = 0;

        foreach (string? str in cookieStrings)
        {
            if (str.StartsWith("$Version", StringComparison.Ordinal))
            {
                version = int.Parse(str[(str.IndexOf('=') + 1)..].Unquote(), CultureInfo.InvariantCulture);
            }
            else if (str.StartsWith("$Path", StringComparison.Ordinal) && current != null)
            {
                current.Path = str[(str.IndexOf('=') + 1)..].Trim();
            }
            else if (str.StartsWith("$Domain", StringComparison.Ordinal) && current != null)
            {
                current.Domain = str[(str.IndexOf('=') + 1)..].Trim();
            }
            else if (str.StartsWith("$Port", StringComparison.Ordinal) && current != null)
            {
                current.Port = str[(str.IndexOf('=') + 1)..].Trim();
            }
            else
            {
                if (current != null)
                {
                    _cookies.Add(current);
                }

                current = new Cookie();
                int idx = str.IndexOf('=');
                if (idx > 0)
                {
                    current.Name = str[..idx].Trim();
                    current.Value = str[(idx + 1)..].Trim();
                }
                else
                {
                    current.Name = str.Trim();
                    current.Value = string.Empty;
                }

                current.Version = version;
            }
        }

        if (current != null)
        {
            _cookies.Add(current);
        }
    }

    private void InitializeQueryString(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return;
        }

        if (query[0] == '?')
        {
            query = query[1..];
        }

        string[] components = query.Split('&');

        foreach (string kv in components)
        {
            int pos = kv.IndexOf('=');
            if (pos == -1)
            {
                QueryString.Add(null, WebUtility.UrlDecode(kv));
            }
            else
            {
                string key = WebUtility.UrlDecode(kv[..pos]);
                string val = WebUtility.UrlDecode(kv[(pos + 1)..]);

                QueryString.Add(key, val);
            }
        }
    }
}