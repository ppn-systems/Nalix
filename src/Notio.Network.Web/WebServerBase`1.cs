using Notio.Network.Web.Enums;
using Notio.Network.Web.Http;
using Notio.Network.Web.Http.Exceptions;
using Notio.Network.Web.Http.Handlers;
using Notio.Network.Web.Internal;
using Notio.Network.Web.MimeTypes;
using Notio.Network.Web.Sessions;
using Notio.Network.Web.Utilities;
using Notio.Network.Web.WebModule;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Web;

/// <summary>
/// BaseValue36 class for <see cref="IWebServer" /> implementations.
/// </summary>
/// <typeparam name="TOptions">The type of the options object used to configure an instance.</typeparam>
/// <seealso cref="IHttpContextHandler" />
/// <seealso cref="IWebServer" />
public abstract class WebServerBase<TOptions> : ConfiguredObject, IWebServer, IHttpContextHandler
    where TOptions : WebServerOptionsBase, new()
{
    private readonly WebModuleCollection _modules;

    private readonly MimeTypeCustomizer _mimeTypeCustomizer = new();

    private ExceptionHandlerCallback _onUnhandledException = ExceptionHandler.Default;
    private HttpExceptionHandlerCallback _onHttpException = HttpExceptionHandler.Default;

    private WebServerState _state = WebServerState.Created;

    private ISessionManager? _sessionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebServerBase{TOptions}" /> class.
    /// </summary>
    protected WebServerBase()
        : this(new TOptions(), null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebServerBase{TOptions}" /> class.
    /// </summary>
    /// <param name="options">A <typeparamref name="TOptions"/> instance that will be used
    /// to configure the server.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    protected WebServerBase(TOptions options)
        : this(Validate.NotNull(nameof(options), options), null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebServerBase{TOptions}" /> class.
    /// </summary>
    /// <param name="configure">A callback that will be used to configure
    /// the server's options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    protected WebServerBase(Action<TOptions> configure)
        : this(new TOptions(), Validate.NotNull(nameof(configure), configure))
    {
    }

    private WebServerBase(TOptions options, Action<TOptions>? configure)
    {
        Options = options;
        LogSource = GetType().Name;
        _modules = new WebModuleCollection(LogSource);

        configure?.Invoke(Options);
        Options.Lock();
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="WebServerBase{TOptions}"/> class.
    /// </summary>
    ~WebServerBase()
    {
        Dispose(false);
    }

    /// <inheritdoc />
    public event WebServerStateChangedEventHandler? StateChanged;

    /// <inheritdoc />
    public IComponentCollection<IWebModule> Modules => _modules;

    /// <summary>
    /// Gets the options object used to configure this instance.
    /// </summary>
    protected TOptions Options { get; }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The server's configuration is locked.</exception>
    /// <exception cref="ArgumentNullException">this property is being set to <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>The default value for this property is <see cref="ExceptionHandler.Default"/>.</para>
    /// </remarks>
    /// <seealso cref="ExceptionHandler"/>
    public ExceptionHandlerCallback OnUnhandledException
    {
        get => _onUnhandledException;
        set
        {
            EnsureConfigurationNotLocked();
            _onUnhandledException = Validate.NotNull(nameof(value), value);
        }
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The server's configuration is locked.</exception>
    /// <exception cref="ArgumentNullException">this property is being set to <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>The default value for this property is <see cref="HttpExceptionHandler.Default"/>.</para>
    /// </remarks>
    /// <seealso cref="HttpExceptionHandler"/>
    public HttpExceptionHandlerCallback OnHttpException
    {
        get => _onHttpException;
        set
        {
            EnsureConfigurationNotLocked();
            _onHttpException = Validate.NotNull(nameof(value), value);
        }
    }

    /// <inheritdoc />
    public ISessionManager? SessionManager
    {
        get => _sessionManager;
        set
        {
            EnsureConfigurationNotLocked();
            _sessionManager = value;
        }
    }

    /// <inheritdoc />
    public WebServerState State
    {
        get => _state;
        private set
        {
            if (value == _state)
            {
                return;
            }

            WebServerState oldState = _state;
            _state = value;

            if (_state != WebServerState.Created)
            {
                LockConfiguration();
            }

            StateChanged?.Invoke(this, new WebServerStateChangedEventArgs(oldState, value));
        }
    }

    /// <summary>
    /// Gets a string to use as a source for log messages.
    /// </summary>
    protected string LogSource { get; }

    /// <inheritdoc />
    public Task HandleContextAsync(IHttpContextImpl context)
    {
        return State > WebServerState.Listening
            ? throw new InvalidOperationException("The web server has already been stopped.")
            : State < WebServerState.Listening
            ? throw new InvalidOperationException("The web server has not been started yet.")
            : DoHandleContextAsync(context);
    }

    string IMimeTypeProvider.GetMimeType(string extension)
    {
        return _mimeTypeCustomizer.GetMimeType(extension);
    }

    bool IMimeTypeProvider.TryDetermineCompression(string mimeType, out bool preferCompression)
    {
        return _mimeTypeCustomizer.TryDetermineCompression(mimeType, out preferCompression);
    }

    /// <inheritdoc />
    public void AddCustomMimeType(string extension, string mimeType)
    {
        _mimeTypeCustomizer.AddCustomMimeType(extension, mimeType);
    }

    /// <inheritdoc />
    public void PreferCompression(string mimeType, bool preferCompression)
    {
        _mimeTypeCustomizer.PreferCompression(mimeType, preferCompression);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The method was already called.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            State = WebServerState.Loading;
            Prepare(cancellationToken);

            _sessionManager?.Start(cancellationToken);
            _modules.StartAll(cancellationToken);

            State = WebServerState.Listening;
            await ProcessRequestsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Debug.WriteLine("Operation canceled.");
        }
        finally
        {
            Trace.WriteLine("Cleaning up", LogSource);
            State = WebServerState.Stopped;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously handles a received request.
    /// </summary>
    /// <param name="context">The context of the request.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    protected async Task DoHandleContextAsync(IHttpContextImpl context)
    {
        context.SupportCompressedRequests = Options.SupportCompressedRequests;
        context.MimeTypeProviders.Push(this);

        try
        {
            Debug.WriteLine(
                $"[{context.Id}] {context.Request.SafeGetRemoteEndpointStr()}: " +
                $"{context.Request.HttpMethod} {context.Request.Url.PathAndQuery} - " +
                $"{context.Request.UserAgent}", LogSource);

            if (SessionManager != null)
            {
                context.Session = new SessionProxy(context, SessionManager);
            }

            try
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    // Return a 404 (Not Found) response if no module handled the response.
                    await _modules.DispatchRequestAsync(context).ConfigureAwait(false);
                    if (!context.IsHandled)
                    {
                        Trace.WriteLine($"[{context.Id}] No module generated a response. Sending 404 - Not Found", LogSource);
                        throw HttpException.NotFound("No module was able to serve the requested path.");
                    }
                }
                catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                {
                    throw; // Let outer catch block handle it
                }
                catch (HttpListenerException)
                {
                    throw; // Let outer catch block handle it
                }
                catch (Exception exception) when (exception is IHttpException)
                {
                    await HttpExceptionHandler.Handle(LogSource, context, exception, _onHttpException)
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    await ExceptionHandler.Handle(LogSource, context, exception, _onUnhandledException, _onHttpException)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                await context.Response.OutputStream.FlushAsync(context.CancellationToken)
                    .ConfigureAwait(false);

                int statusCode = context.Response.StatusCode;
                string statusDescription = context.Response.StatusDescription;
                bool sendChunked = context.Response.SendChunked;
                long contentLength = context.Response.ContentLength64;
                context.Close();
                string sanitizedHttpMethod = context.Request.HttpMethod
                    .Replace(Environment.NewLine, "")
                    .Replace("\n", "")
                    .Replace("\r", "");

                string sanitizedUrlPath = context.Request.Url.AbsolutePath
                    .Replace(Environment.NewLine, "")
                    .Replace("\n", "")
                    .Replace("\r", "");

                Trace.WriteLine(
                    $"[{context.Id}] {sanitizedHttpMethod} {sanitizedUrlPath}: " +
                    $"\"{statusCode} {statusDescription}\" sent in {context.Age}ms " +
                    $"({(sendChunked ? "chunked" :
                    $"{contentLength.ToString(CultureInfo.InvariantCulture)} bytes")})", LogSource);
            }
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            Debug.WriteLine($"[{context.Id}] Operation canceled.", LogSource);
        }
        catch (HttpListenerException ex)
        {
            Trace.WriteLine($"[{context.Id}] Listener exception: {ex.Message}", LogSource);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[{context.Id}] Unhandled exception: {ex}", LogSource);
            OnFatalException();
        }
    }

    /// <inheritdoc />
    protected override void OnBeforeLockConfiguration()
    {
        base.OnBeforeLockConfiguration();

        _mimeTypeCustomizer.Lock();
        _modules.Lock();
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _modules.Dispose();
    }

    /// <summary>
    /// Prepares a web server for running.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to stop the web server.</param>
    protected virtual void Prepare(CancellationToken cancellationToken)
    {
    }

    /// <summary>
    /// <para>Asynchronously receives requests and processes them.</para>
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to stop the web server.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    protected abstract Task ProcessRequestsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// <para>Called when an exception is caught in the web server's request processing loop.</para>
    /// <para>This method should tell the server socket to stop accepting further requests.</para>
    /// </summary>
    protected abstract void OnFatalException();
}
