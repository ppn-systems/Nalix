using System;
using System.Globalization;

namespace Notio.Network.Web.Net.Internal;

internal sealed class ListenerPrefix
{
    public ListenerPrefix(string uri)
    {
        int defaultPort = 80;

        if (uri.StartsWith("https://", StringComparison.Ordinal))
        {
            defaultPort = 443;
            Secure = true;
        }

        int length = uri.Length;
        int startHost = uri.IndexOf(':') + 3;

        if (startHost >= length)
        {
            throw new ArgumentException("No host specified.");
        }

        int colon = uri.LastIndexOf(':');
        int root;

        if (colon > 0)
        {
            Host = uri[startHost..colon];
            root = uri.IndexOf('/', colon, length - colon);
            Port = int.Parse(uri.Substring(colon + 1, root - colon - 1), CultureInfo.InvariantCulture);
        }
        else
        {
            root = uri.IndexOf('/', startHost, length - startHost);
            Host = uri[startHost..root];
            Port = defaultPort;
        }

        Path = uri[root..];

        if (Path.Length != 1)
        {
            Path = Path[..^1];
        }
    }

    public HttpListener? Listener { get; set; }

    public bool Secure { get; }

    public string Host { get; }

    public int Port { get; }

    public string Path { get; }

    public static void CheckUri(string uri)
    {
        if (uri == null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        if (!uri.StartsWith("http://", StringComparison.Ordinal) && !uri.StartsWith("https://", StringComparison.Ordinal))
        {
            throw new ArgumentException("Only 'http' and 'https' schemes are supported.");
        }

        int length = uri.Length;
        int startHost = uri.IndexOf(':') + 3;

        if (startHost >= length)
        {
            throw new ArgumentException("No host specified.");
        }

        int colon = uri[startHost..].IndexOf(':') > 0 ? uri.LastIndexOf(':') : -1;

        if (startHost == colon)
        {
            throw new ArgumentException("No host specified.");
        }

        int root;
        if (colon > 0)
        {
            root = uri.IndexOf('/', colon, length - colon);
            if (root == -1)
            {
                throw new ArgumentException("No path specified.");
            }

            if (!int.TryParse(uri.Substring(colon + 1, root - colon - 1), out int p) || p <= 0 || p >= 65536)
            {
                throw new ArgumentException("Invalid port.");
            }
        }
        else
        {
            root = uri.IndexOf('/', startHost, length - startHost);
            if (root == -1)
            {
                throw new ArgumentException("No path specified.");
            }
        }

        if (uri[^1] != '/')
        {
            throw new ArgumentException("The prefix must end with '/'");
        }
    }

    public bool IsValid()
    {
        return Path.IndexOf('%') == -1 && Path.IndexOf("//", StringComparison.Ordinal) == -1;
    }

    public override string ToString()
    {
        return $"{Host}:{Port} ({(Secure ? "Secure" : "Insecure")}";
    }
}