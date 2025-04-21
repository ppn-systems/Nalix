using Nalix.Network.Web.Http.Handlers;

namespace Nalix.Network.Web.WebModule;

/// <summary>
/// Provides extension methods for types implementing <see cref="IWebModule"/>.
/// </summary>
public static class WebModuleExtensions
{
    /// <summary>
    /// Sets the HTTP exception handler on an <see cref="IWebModule" />.
    /// </summary>
    /// <typeparam name="TWebModule">The type of the web server.</typeparam>
    /// <param name="this">The <typeparamref name="TWebModule" /> on which this method is called.</param>
    /// <param name="handler">The HTTP exception handler.</param>
    /// <returns><paramref name="this"/> with the <see cref="IWebModule.OnHttpException">OnHttpException</see>
    /// property set to <paramref name="handler" />.</returns>
    /// <exception cref="System.NullReferenceException"><paramref name="this" /> is <see langword="null" />.</exception>
    /// <exception cref="System.InvalidOperationException">The module's configuration is locked.</exception>
    /// <seealso cref="IWebModule.OnHttpException" />
    /// <seealso cref="HttpExceptionHandler" />
    public static TWebModule HandleHttpException<TWebModule>(this TWebModule @this, HttpExceptionHandlerCallback handler)
        where TWebModule : IWebModule
    {
        @this.OnHttpException = handler;
        return @this;
    }

    /// <summary>
    /// Sets the unhandled exception handler on an <see cref="IWebModule" />.
    /// </summary>
    /// <typeparam name="TWebModule">The type of the web server.</typeparam>
    /// <param name="this">The <typeparamref name="TWebModule" /> on which this method is called.</param>
    /// <param name="handler">The unhandled exception handler.</param>
    /// <returns><paramref name="this"/> with the <see cref="IWebModule.OnUnhandledException">OnUnhandledException</see>
    /// property set to <paramref name="handler" />.</returns>
    /// <exception cref="System.NullReferenceException"><paramref name="this" /> is <see langword="null" />.</exception>
    /// <exception cref="System.InvalidOperationException">The module's configuration is locked.</exception>
    /// <seealso cref="IWebModule.OnUnhandledException" />
    /// <seealso cref="Http.Exceptions.ExceptionHandler" />
    public static TWebModule HandleUnhandledException<TWebModule>(this TWebModule @this, ExceptionHandlerCallback handler)
        where TWebModule : IWebModule
    {
        @this.OnUnhandledException = handler;
        return @this;
    }
}
