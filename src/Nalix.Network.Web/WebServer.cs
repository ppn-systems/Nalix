using Nalix.Network.Web.Enums;
using Nalix.Network.Web.Http;
using Nalix.Network.Web.Net.Internal;
using Nalix.Network.Web.Routing;
using Nalix.Network.Web.Utilities;
using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Network.Web;

/// <summary>
/// <para>Notio's web server. This is the default implementation of <see cref="IWebServer"/>.</para>
/// <para>This class also contains some useful constants related to Notio's internal working.</para>
/// </summary>
public partial class WebServer : WebServerBase<WebServerOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebServer"/> class,
    /// that will respond on HTTP port 80 on all network interfaces.
    /// </summary>
    public WebServer()
        : this(80)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebServer"/> class,
    /// that will respond on the specified HTTP port on all network interfaces.
    /// </summary>
    /// <param name="port">The port.</param>
    private WebServer(int port)
        : this($"http" + $"://*:{port}/")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebServer"/> class
    /// with the specified URL prefixes.
    /// </summary>
    /// <param name="urlPrefixes">The URL prefixes to configure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="urlPrefixes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <para>One or more of the elements of <paramref name="urlPrefixes"/> is the empty string.</para>
    /// <para>- or -</para>
    /// <para>One or more of the elements of <paramref name="urlPrefixes"/> is already registered.</para>
    /// </exception>
    private WebServer(params string[] urlPrefixes)
        : this(new WebServerOptions().WithUrlPrefixes(urlPrefixes))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebServer" /> class.
    /// </summary>
    /// <param name="mode">The type of HTTP listener to configure.</param>
    /// <param name="urlPrefixes">The URL prefixes to configure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="urlPrefixes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <para>One or more of the elements of <paramref name="urlPrefixes"/> is the empty string.</para>
    /// <para>- or -</para>
    /// <para>One or more of the elements of <paramref name="urlPrefixes"/> is already registered.</para>
    /// </exception>
    public WebServer(HttpListenerMode mode, params string[] urlPrefixes)
        : this(new WebServerOptions().WithMode(mode).WithUrlPrefixes(urlPrefixes))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebServer" /> class.
    /// </summary>
    /// <param name="mode">The type of HTTP listener to configure.</param>
    /// <param name="certificate">The X.509 certificate to use for SSL connections.</param>
    /// <param name="urlPrefixes">The URL prefixes to configure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="urlPrefixes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <para>One or more of the elements of <paramref name="urlPrefixes"/> is the empty string.</para>
    /// <para>- or -</para>
    /// <para>One or more of the elements of <paramref name="urlPrefixes"/> is already registered.</para>
    /// </exception>
    public WebServer(HttpListenerMode mode, X509Certificate2 certificate, params string[] urlPrefixes)
        : this(new WebServerOptions()
            .WithMode(mode)
            .WithCertificate(certificate)
            .WithUrlPrefixes(urlPrefixes))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebServer"/> class.
    /// </summary>
    /// <param name="options">A <see cref="WebServerOptions"/> object used to configure this instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    private WebServer(WebServerOptions options)
        : base(options)
    {
        Listener = CreateHttpListener();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebServer"/> class.
    /// </summary>
    /// <param name="configure">A callback that will be used to configure
    /// the server's options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    public WebServer(Action<WebServerOptions> configure)
        : base(configure)
    {
        Listener = CreateHttpListener();
    }

    /// <summary>
    /// Gets the underlying HTTP listener.
    /// </summary>
    private IHttpListener Listener { get; }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                Listener.Dispose();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Exception thrown while disposing HTTP listener. {ex}", LogSource);
            }

            Trace.WriteLine("Listener closed.", LogSource);
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void Prepare(CancellationToken cancellationToken)
    {
        Listener.Start();
        Trace.WriteLine("HTTP Listener started.", LogSource);

        // close port when the cancellation token is cancelled
        _ = cancellationToken.Register(() => Listener.Stop());
    }

    /// <inheritdoc />
    protected override async Task ProcessRequestsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && (Listener.IsListening))
        {
            IHttpContextImpl context = await Listener.GetContextAsync(cancellationToken).ConfigureAwait(false);
            context.CancellationToken = cancellationToken;
            context.Route = RouteMatch.UnsafeFromRoot(UrlPath.Normalize(context.Request.Url.AbsolutePath, false));
            _ = Task.Run(() => DoHandleContextAsync(context), cancellationToken);
        }
    }

    /// <inheritdoc />
    protected override void OnFatalException()
    {
        Listener.Dispose();
    }

    private IHttpListener CreateHttpListener()
    {
        IHttpListener DoCreate()
        {
            return Options.Mode switch
            {
                HttpListenerMode.Microsoft => System.Net.HttpListener.IsSupported
                    ? new SystemHttpListener(new System.Net.HttpListener())
                    : new Net.HttpListener(Options.Certificate),
                _ => new Net.HttpListener(Options.Certificate)
            };
        }

        IHttpListener listener = DoCreate();
        Trace.WriteLine($"Running HTTPListener: {listener.Name}", LogSource);

        foreach (string prefix in Options.UrlPrefixes)
        {
            string urlPrefix = new(prefix.ToCharArray());

            if (!urlPrefix.EndsWith('/'))
            {
                urlPrefix += '/';
            }

            urlPrefix = urlPrefix.ToLowerInvariant();

            listener.AddPrefix(urlPrefix);
            Trace.WriteLine($"Web server prefix '{urlPrefix}' added.", LogSource);
        }

        return listener;
    }
}
